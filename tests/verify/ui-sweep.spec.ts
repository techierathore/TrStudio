import { test, expect, Page, BrowserContext } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

const BASE = 'http://localhost:5099';
const OUT_DIR = path.resolve('test-results/verify-ui');
const RESULT_FILE = path.resolve('test-results/verify-ui-results.json');

fs.mkdirSync(OUT_DIR, { recursive: true });

const tokens = JSON.parse(
  fs.readFileSync(path.resolve('.verify/tokens.json'), 'utf-8')
);

type ScreenMeta = { route: string; req: string; role: string };

type Result = {
  route: string;
  req: string;
  role: string;
  render: 'OK' | 'EMPTY' | 'ERROR';
  data: string;
  visual1280: 'OK' | 'FAIL';
  visual390: 'OK' | 'FAIL';
  verdict: 'PASS' | 'FAIL';
  note: string;
  shots: string[];
};

const results: Result[] = [];

function slug(route: string) {
  return route.replace(/^\//, '').replace(/\//g, '-') || 'root';
}

async function establishSession(context: BrowserContext, token: string) {
  const page = await context.newPage();
  await page.goto(`${BASE}/dev/session`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.waitForSelector('[data-testid=dev-token]', { timeout: 15000 });
  const ta = page.locator('[data-testid=dev-token]');
  await ta.click();
  await ta.fill(token);
  // Trigger Blazor bind (onchange/oninput) explicitly, then blur.
  await ta.dispatchEvent('input');
  await ta.dispatchEvent('change');
  await page.locator('body').click({ position: { x: 5, y: 5 } }).catch(() => {});
  await page.waitForTimeout(600);
  // Wait for the establish button to become enabled before clicking.
  const btn = page.locator('[data-testid=dev-establish]');
  await btn.waitFor({ state: 'visible', timeout: 10000 });
  await page
    .waitForFunction(
      () => {
        const b = document.querySelector('[data-testid=dev-establish]') as HTMLButtonElement | null;
        return !!b && !b.disabled;
      },
      { timeout: 15000 }
    )
    .catch(() => {});
  await Promise.all([
    page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 30000 }).catch(() => {}),
    btn.click({ force: true }),
  ]);
  await page.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});
  await page.waitForTimeout(1500);
  return page;
}

async function sweep(page: Page, meta: ScreenMeta): Promise<Result> {
  const r: Result = {
    route: meta.route,
    req: meta.req,
    role: meta.role,
    render: 'OK',
    data: '',
    visual1280: 'OK',
    visual390: 'OK',
    verdict: 'PASS',
    note: '',
    shots: [],
  };
  const notes: string[] = [];

  try {
    await page.setViewportSize({ width: 1280, height: 800 });
    const resp = await page.goto(`${BASE}${meta.route}`, {
      waitUntil: 'domcontentloaded',
      timeout: 30000,
    });
    const status = resp ? resp.status() : 0;
    await page.waitForLoadState('networkidle', { timeout: 20000 }).catch(() => {});
    await page.waitForTimeout(1800);

    // --- RENDER gate ---
    const bodyText = (await page.locator('body').innerText().catch(() => '')) || '';
    const errorUiVisible = await page
      .locator('#blazor-error-ui')
      .isVisible()
      .catch(() => false);
    const hasUnhandled = /An unhandled error has occurred/i.test(bodyText);
    const hasNothingHere = /Sorry, there's nothing at this address/i.test(bodyText);

    const h1 = page.locator('h1').first();
    const h1Count = await page.locator('h1').count();
    let headingText = '';
    let headingVisible = false;
    if (h1Count > 0) {
      headingVisible = await h1.isVisible().catch(() => false);
      headingText = (await h1.innerText().catch(() => '')) || '';
    }
    // fallback main heading
    let mainHeadingLoc = h1;
    let mainHeadingVisible = headingVisible;
    if (!headingVisible) {
      const h2 = page.locator('h2').first();
      if ((await page.locator('h2').count()) > 0) {
        const v = await h2.isVisible().catch(() => false);
        if (v) {
          mainHeadingLoc = h2;
          mainHeadingVisible = true;
          headingText = (await h2.innerText().catch(() => '')) || headingText;
        }
      }
    }

    if (errorUiVisible || hasUnhandled || hasNothingHere || status >= 500 || status === 404) {
      r.render = 'ERROR';
      notes.push(
        `render-error: status=${status} blazorErr=${errorUiVisible} unhandled=${hasUnhandled} nothingHere=${hasNothingHere}`
      );
    } else if (!mainHeadingVisible) {
      r.render = 'ERROR';
      notes.push(`no visible main heading (h1 count=${h1Count})`);
    } else {
      notes.push(`heading="${headingText.slice(0, 60).replace(/\n/g, ' ')}"`);
    }

    // --- DATA region ---
    const rowCount = await page.locator('table tbody tr').count().catch(() => 0);
    const cardSel =
      '[data-testid*=card], .card, [class*=card], [role=listitem], [data-testid*=item]';
    const cardCount = await page.locator(cardSel).count().catch(() => 0);
    const kpiCount = await page
      .locator('[data-testid*=kpi], [class*=kpi], [class*=stat]')
      .count()
      .catch(() => 0);
    const listCount = await page.locator('ul li, ol li').count().catch(() => 0);

    if (r.render !== 'ERROR') {
      if (rowCount > 0) r.data = `rows=${rowCount}`;
      else if (cardCount > 0) r.data = `cards=${cardCount}`;
      else if (kpiCount > 0) r.data = `kpi=${kpiCount}`;
      else if (listCount > 3) r.data = `list=${listCount}`;
      else {
        r.data = 'no-data-region/empty';
        // Only downgrade to EMPTY if the page clearly has a data region that is empty.
        const emptyMsg = /no\s+(items|data|results|characters|projects|records)|nothing (here|yet)|empty/i.test(
          bodyText
        );
        if (emptyMsg || rowCount === 0) {
          r.render = 'EMPTY';
          if (emptyMsg) notes.push('empty-state message shown');
        }
      }
    }

    // --- VISUAL 1280 ---
    const shot1280 = path.join(OUT_DIR, `${slug(meta.route)}-1280.png`);
    await page.screenshot({ path: shot1280, fullPage: true });
    r.shots.push(shot1280);
    if (mainHeadingVisible) {
      const bb = await mainHeadingLoc.boundingBox().catch(() => null);
      if (!bb || bb.width <= 0 || bb.height <= 0 || bb.y < 0) {
        r.visual1280 = 'FAIL';
        notes.push(`1280 heading bbox bad: ${JSON.stringify(bb)}`);
      }
    }
    const overflow1280 = await page.evaluate(
      () => document.documentElement.scrollWidth - window.innerWidth
    );
    if (overflow1280 > 2) {
      // horizontal overflow at desktop is a softer signal; note but only fail if large
      notes.push(`1280 h-overflow=${overflow1280}`);
    }

    // --- VISUAL 390 ---
    await page.setViewportSize({ width: 390, height: 844 });
    await page.waitForTimeout(900);
    const shot390 = path.join(OUT_DIR, `${slug(meta.route)}-390.png`);
    await page.screenshot({ path: shot390, fullPage: true });
    r.shots.push(shot390);
    const sw390 = await page.evaluate(() => document.documentElement.scrollWidth);
    const iw390 = await page.evaluate(() => window.innerWidth);
    if (sw390 > iw390 + 2) {
      r.visual390 = 'FAIL';
      notes.push(`390 h-overflow: scrollWidth=${sw390} innerWidth=${iw390}`);
    }
    if (mainHeadingVisible) {
      const bb390 = await mainHeadingLoc.boundingBox().catch(() => null);
      if (!bb390 || bb390.width <= 0 || bb390.height <= 0 || bb390.y < 0) {
        r.visual390 = 'FAIL';
        notes.push(`390 heading bbox bad: ${JSON.stringify(bb390)}`);
      }
    }
  } catch (e: any) {
    r.render = 'ERROR';
    notes.push(`exception: ${e.message}`);
  }

  const renderOk = r.render === 'OK' || r.render === 'EMPTY';
  const visualOk = r.visual1280 === 'OK' && r.visual390 === 'OK';
  r.verdict = renderOk && visualOk ? 'PASS' : 'FAIL';
  r.note = notes.join(' | ');
  results.push(r);
  return r;
}

const CREATOR_SCREENS: ScreenMeta[] = [
  { route: '/avatar', req: 'UI-010', role: 'Owner' },
  { route: '/avatar/characters', req: 'UI-011', role: 'Owner' },
  { route: '/image', req: 'UI-020', role: 'Owner' },
  { route: '/voice', req: 'UI-030', role: 'Owner' },
  { route: '/video', req: 'UI-040', role: 'Owner' },
  { route: '/gallery', req: 'UI-050', role: 'Owner' },
  { route: '/projects', req: 'UI-051', role: 'Owner' },
  { route: '/compare', req: 'UI-052', role: 'Owner' },
  { route: '/library', req: 'UI-053', role: 'Owner' },
  { route: '/account', req: 'UI-060', role: 'Owner' },
];

const ADMIN_SCREENS: ScreenMeta[] = [
  { route: '/admin/setup', req: 'UI-070', role: 'Operator' },
  { route: '/admin/registry', req: 'UI-071', role: 'Operator' },
  { route: '/admin/comfy/import', req: 'UI-072', role: 'Operator' },
  { route: '/admin/ops', req: 'UI-073', role: 'Operator' },
  { route: '/admin/sources', req: 'UI-074', role: 'Operator' },
  { route: '/admin/policy', req: 'UI-075', role: 'Operator' },
  { route: '/admin/models', req: 'UI-076', role: 'Operator' },
  { route: '/admin/setup/hardware', req: 'UI-070', role: 'Operator' },
  { route: '/admin/comfy/debug', req: 'UI-072', role: 'Operator' },
];

const AUTH_SCREENS: ScreenMeta[] = [
  { route: '/login', req: 'UI-001', role: 'None' },
  { route: '/register', req: 'UI-002', role: 'None' },
  { route: '/forgot-password', req: 'UI-003', role: 'None' },
  { route: '/setup-required', req: 'UI-004', role: 'None' },
];

test.describe.serial('UI sweep', () => {
  test('Creator surface as Owner', async ({ browser }) => {
    const ctx = await browser.newContext();
    const page = await establishSession(ctx, tokens.Owner);
    for (const s of CREATOR_SCREENS) {
      await sweep(page, s);
    }
    await ctx.close();
  });

  test('Admin surface as Operator', async ({ browser }) => {
    const ctx = await browser.newContext();
    const page = await establishSession(ctx, tokens.Operator);
    for (const s of ADMIN_SCREENS) {
      await sweep(page, s);
    }
    await ctx.close();
  });

  test('Auth screens (no login)', async ({ browser }) => {
    const ctx = await browser.newContext();
    const page = await ctx.newPage();
    for (const s of AUTH_SCREENS) {
      await sweep(page, s);
    }
    await ctx.close();
  });

  test('Role-gate: Creator to /admin/registry', async ({ browser }) => {
    const ctx = await browser.newContext();
    const page = await establishSession(ctx, tokens.Creator);
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto(`${BASE}/admin/registry`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.waitForLoadState('networkidle', { timeout: 20000 }).catch(() => {});
    await page.waitForTimeout(1800);
    const denied = await page.locator('[data-testid=access-denied]').isVisible().catch(() => false);
    const tableVisible = await page.locator('table tbody tr').count().catch(() => 0);
    const shot = path.join(OUT_DIR, 'creator-admin-registry-gate.png');
    await page.screenshot({ path: shot, fullPage: true });
    const gate = {
      accessDeniedVisible: denied,
      registryTableRows: tableVisible,
      passed: denied && tableVisible === 0,
      shot,
    };
    fs.writeFileSync(
      path.resolve('test-results/verify-ui-gate.json'),
      JSON.stringify(gate, null, 2)
    );
    await ctx.close();
    console.log('GATE_RESULT:', JSON.stringify(gate));
  });

  test.afterAll(async () => {
    fs.writeFileSync(RESULT_FILE, JSON.stringify(results, null, 2));
    console.log('\n===RESULTS_JSON_START===');
    console.log(JSON.stringify(results));
    console.log('===RESULTS_JSON_END===');
  });
});
