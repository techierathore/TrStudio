const { chromium } = require('@playwright/test');
const fs = require('fs');
const tokens = JSON.parse(fs.readFileSync('.verify/tokens.json', 'utf8'));
const BASE = 'http://localhost:5099';

async function establish(ctx, token) {
  const p = await ctx.newPage();
  await p.goto(`${BASE}/dev/session`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await p.waitForSelector('[data-testid=dev-token]', { timeout: 15000 });
  const ta = p.locator('[data-testid=dev-token]');
  await ta.click(); await ta.fill(token);
  await ta.dispatchEvent('input'); await ta.dispatchEvent('change');
  await p.locator('body').click({ position: { x: 5, y: 5 } }).catch(() => {});
  await p.waitForFunction(() => { const b = document.querySelector('[data-testid=dev-establish]'); return b && !b.disabled; }, { timeout: 15000 }).catch(() => {});
  await Promise.all([p.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 30000 }).catch(() => {}), p.locator('[data-testid=dev-establish]').click({ force: true })]);
  await p.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});
  await p.waitForTimeout(1200);
  return p;
}

async function probe(p, route) {
  await p.setViewportSize({ width: 390, height: 844 });
  await p.goto(`${BASE}${route}`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await p.waitForLoadState('networkidle', { timeout: 20000 }).catch(() => {});
  await p.waitForTimeout(2000);
  const info = await p.evaluate(() => {
    const de = document.documentElement;
    const iw = window.innerWidth;
    // find widest offending elements
    const offenders = [];
    const all = document.querySelectorAll('*');
    for (const el of all) {
      const r = el.getBoundingClientRect();
      if (r.right > iw + 2 && r.width > 0 && r.width < 4000) {
        offenders.push({
          tag: el.tagName.toLowerCase(),
          cls: (el.className && el.className.toString().slice(0, 50)) || '',
          testid: el.getAttribute('data-testid') || '',
          right: Math.round(r.right),
          width: Math.round(r.width),
        });
      }
    }
    // dedupe by right, keep widest few
    offenders.sort((a, b) => b.right - a.right);
    return {
      docScrollWidth: de.scrollWidth,
      bodyScrollWidth: document.body.scrollWidth,
      innerWidth: iw,
      offenders: offenders.slice(0, 6),
    };
  });
  console.log(`\n=== ${route} ===`);
  console.log(`docScrollWidth=${info.docScrollWidth} bodyScrollWidth=${info.bodyScrollWidth} innerWidth=${info.innerWidth} => overflow=${info.docScrollWidth - info.innerWidth}`);
  for (const o of info.offenders) console.log(`   right=${o.right} w=${o.width} <${o.tag} class="${o.cls}" testid="${o.testid}">`);
}

(async () => {
  const b = await chromium.launch({ headless: true });
  // Owner routes
  const c1 = await b.newContext();
  const p1 = await establish(c1, tokens.Owner);
  for (const r of ['/account', '/avatar/characters', '/compare', '/library']) await probe(p1, r);
  await c1.close();
  // Operator routes
  const c2 = await b.newContext();
  const p2 = await establish(c2, tokens.Operator);
  for (const r of ['/admin/sources', '/admin/ops', '/admin/registry']) await probe(p2, r);
  await c2.close();
  await b.close();
})().catch(e => { console.error('ERR', e.message); process.exit(1); });
