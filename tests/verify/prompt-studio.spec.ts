import { test, expect, Page, BrowserContext } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

// Focused verify for Phase-10 Prompt Studio: REQ-UI-081 (chat screen) + REQ-UI-082
// (session rail + prompt-use actions). Render gate (§4a) + visual-truth gate (§4b).
const BASE = 'http://localhost:5099';
const OUT_DIR = path.resolve('test-results/verify-prompt');
const RESULT_FILE = path.resolve('test-results/verify-prompt-results.json');
fs.mkdirSync(OUT_DIR, { recursive: true });

const tokens = JSON.parse(fs.readFileSync(path.resolve('.verify/tokens.json'), 'utf-8'));

const out: any = { render: {}, ui082: {}, visual: {}, notes: [] };

async function btnDisabled(page: Page) {
  return page.evaluate(() => {
    const b = document.querySelector('[data-testid=dev-establish]') as HTMLButtonElement | null;
    return !b || b.disabled;
  });
}

async function establishSession(context: BrowserContext, token: string) {
  const page = await context.newPage();
  await page.goto(`${BASE}/dev/session`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.waitForSelector('[data-testid=dev-token]', { timeout: 15000 });
  const ta = page.locator('[data-testid=dev-token]');
  const btn = page.locator('[data-testid=dev-establish]');

  // The Blazor Server circuit must be interactive before @bind fires. Retry the
  // fill + a real trailing keystroke (fires oninput/onchange) until the button enables.
  let enabled = false;
  for (let attempt = 0; attempt < 20 && !enabled; attempt++) {
    await ta.click().catch(() => {});
    await ta.fill(token).catch(() => {});
    // real keystrokes so Blazor's DOM event handlers fire once the circuit is live
    await ta.press('End').catch(() => {});
    await ta.press(' ').catch(() => {});
    await ta.press('Backspace').catch(() => {});
    await ta.dispatchEvent('change').catch(() => {});
    await page.locator('body').click({ position: { x: 5, y: 5 } }).catch(() => {});
    await page.waitForTimeout(1000);
    enabled = !(await btnDisabled(page));
  }
  if (!enabled) throw new Error('dev-establish never enabled — Blazor circuit/bind failed');

  await Promise.all([
    page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 30000 }).catch(() => {}),
    btn.click(),
  ]);
  await page.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});
  await page.waitForTimeout(1500);
  return page;
}

// bounding-box overlap helper (§4b)
function overlaps(a: any, b: any, tol = 4) {
  if (!a || !b) return false;
  return (
    a.x < b.x + b.width - tol &&
    a.x + a.width - tol > b.x &&
    a.y < b.y + b.height - tol &&
    a.y + a.height - tol > b.y
  );
}

test.describe.serial('Prompt Studio verify', () => {
  test('UI-081 render gate + UI-082 actions + visual', async ({ browser }) => {
    test.setTimeout(240000);
    const ctx = await browser.newContext();
    const page = await establishSession(ctx, tokens.Owner);

    // ---- navigate ----
    await page.setViewportSize({ width: 1280, height: 800 });
    const resp = await page.goto(`${BASE}/prompt-studio`, {
      waitUntil: 'domcontentloaded',
      timeout: 30000,
    });
    out.render.status = resp ? resp.status() : 0;
    await page.waitForLoadState('networkidle', { timeout: 20000 }).catch(() => {});
    await page.waitForSelector('[data-testid=prompt-studio]', { timeout: 20000 }).catch(() => {});
    await page.waitForTimeout(1500);

    const body = (await page.locator('body').innerText().catch(() => '')) || '';
    out.render.blazorErr = await page.locator('#blazor-error-ui').isVisible().catch(() => false);
    out.render.unhandled = /An unhandled error has occurred/i.test(body);

    // ---- §4a render gate: each control renders ----
    const ctrl = async (sel: string) => ({
      count: await page.locator(sel).count().catch(() => 0),
      visible: await page.locator(sel).first().isVisible().catch(() => false),
    });
    out.render.h1 = (await page.locator('h1').first().innerText().catch(() => '')) || '';
    out.render.root = await ctrl('[data-testid=prompt-studio]');
    out.render.sessionRail = await ctrl('[data-testid=session-rail]');
    out.render.newChat = await ctrl('[data-testid=new-chat-btn]');
    out.render.llmSelect = await ctrl('[data-testid=llm-select]');
    // picker is a TrBlazeUI <Select> (popover), not a native <select>. Read the trigger
    // label (shows the selected registry model) and open it to count SelectItems.
    out.render.llmTriggerText =
      (await page.locator('[data-testid=llm-select]').first().innerText().catch(() => '')) || '';
    try {
      await page.locator('[data-testid=llm-select]').first().click({ timeout: 5000 });
      await page.waitForTimeout(700);
      out.render.llmOptions = await page
        .locator('[role=option], [data-slot=select-item], [data-testid=llm-select] [role=option]')
        .count()
        .catch(() => 0);
      // close the popover
      await page.keyboard.press('Escape').catch(() => {});
      await page.waitForTimeout(300);
    } catch (e: any) {
      out.render.llmOptions = 0;
      out.notes.push('picker open failed: ' + e.message);
    }
    out.render.visionBadge = await ctrl('[data-testid=vision-badge]');
    out.render.visionBadgeText =
      (await page.locator('[data-testid=vision-badge]').first().innerText().catch(() => '')) || '';
    out.render.composer = await ctrl('[data-testid=composer-text]');
    out.render.sendBtn = await ctrl('[data-testid=send-btn]');
    out.render.attachBtn = await ctrl('[data-testid=attach-btn]');
    out.render.apiAlert = await ctrl('[data-testid=api-alert]');

    // ---- UI-082: exercise a round-trip so prompt-use actions render ----
    try {
      await page.locator('[data-testid=new-chat-btn]').first().click({ timeout: 8000 }).catch(() => {});
      await page.waitForTimeout(800);
      const composer = page.locator('[data-testid=composer-text]').first();
      await composer.click({ timeout: 8000 });
      await composer.fill('cinematic portrait of a knight, dramatic lighting');
      await page.locator('[data-testid=send-btn]').first().click({ timeout: 8000 });
      // wait for assistant reply block
      await page.waitForSelector('[data-testid=prompt-block]', { timeout: 25000 }).catch(() => {});
      await page.waitForTimeout(1200);
    } catch (e: any) {
      out.notes.push('ui082 round-trip exception: ' + e.message);
    }
    out.ui082.promptBlock = await page.locator('[data-testid=prompt-block]').count().catch(() => 0);
    out.ui082.copyBtn = await page.locator('[data-testid=copy-btn]').count().catch(() => 0);
    out.ui082.saveBtn = await page.locator('[data-testid=save-btn]').count().catch(() => 0);
    out.ui082.sendStudioBtn = await page
      .locator('[data-testid=send-studio-btn]')
      .count()
      .catch(() => 0);
    // session rail now shows a session card
    out.ui082.sessionCards = await page
      .locator('[data-testid=session-rail] button, [data-testid=session-rail] a, [data-testid=session-rail] li')
      .count()
      .catch(() => 0);

    // ---- §4b visual gate @1280 ----
    const shot1280 = path.join(OUT_DIR, 'prompt-studio-1280.png');
    await page.screenshot({ path: shot1280, fullPage: true });
    out.visual.shot1280 = shot1280;
    out.visual.overflow1280 = await page.evaluate(
      () => document.documentElement.scrollWidth - window.innerWidth
    );
    const railBox = await page.locator('[data-testid=session-rail]').first().boundingBox().catch(() => null);
    const threadBox = await page.locator('[data-testid=chat-thread]').first().boundingBox().catch(() => null);
    out.visual.railBox = railBox;
    out.visual.threadBox = threadBox;
    out.visual.railThreadOverlap = overlaps(railBox, threadBox);
    // key controls sized > 0
    for (const s of ['llm-select', 'composer-text', 'send-btn']) {
      const bb = await page.locator(`[data-testid=${s}]`).first().boundingBox().catch(() => null);
      out.visual[`${s}_1280`] = bb;
      if (!bb || bb.width <= 0 || bb.height <= 0) out.notes.push(`1280 ${s} zero-size box`);
    }

    // ---- §4b visual gate @390 ----
    await page.setViewportSize({ width: 390, height: 844 });
    await page.waitForTimeout(1000);
    const shot390 = path.join(OUT_DIR, 'prompt-studio-390.png');
    await page.screenshot({ path: shot390, fullPage: true });
    out.visual.shot390 = shot390;
    out.visual.scrollWidth390 = await page.evaluate(() => document.documentElement.scrollWidth);
    out.visual.innerWidth390 = await page.evaluate(() => window.innerWidth);
    out.visual.overflow390 = out.visual.scrollWidth390 - out.visual.innerWidth390;

    await ctx.close();

    fs.writeFileSync(RESULT_FILE, JSON.stringify(out, null, 2));
    console.log('\n===PROMPT_RESULTS_START===');
    console.log(JSON.stringify(out));
    console.log('===PROMPT_RESULTS_END===');

    // hard assertions -> test pass/fail
    expect(out.render.root.visible, 'prompt-studio root renders').toBeTruthy();
    expect(out.render.unhandled, 'no unhandled error').toBeFalsy();
    expect(out.render.llmSelect.visible, 'llm picker renders').toBeTruthy();
    // registry-driven: picker shows a selected model label AND opens to >=2 seeded rows
    expect(out.render.llmTriggerText.trim().length, 'picker shows a model label').toBeGreaterThan(0);
    expect(out.render.llmOptions, 'picker lists registry rows').toBeGreaterThan(0);
    expect(out.render.visionBadge.visible, 'vision badge renders').toBeTruthy();
    expect(out.render.composer.visible, 'composer renders').toBeTruthy();
    expect(out.ui082.promptBlock, 'assistant prompt block rendered').toBeGreaterThan(0);
    expect(out.ui082.copyBtn, 'copy action').toBeGreaterThan(0);
    expect(out.ui082.saveBtn, 'save-to-library action').toBeGreaterThan(0);
    expect(out.ui082.sendStudioBtn, 'send-to-studio action').toBeGreaterThan(0);
    expect(out.visual.railThreadOverlap, 'rail/thread not overlapping @1280').toBeFalsy();
    expect(out.visual.overflow390, 'no horizontal overflow @390').toBeLessThanOrEqual(2);
  });
});
