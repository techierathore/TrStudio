import { test, expect, Page, APIRequestContext } from '@playwright/test';
import * as http from 'http';
import { execFileSync } from 'child_process';
import * as path from 'path';

// REQ-FN-009 and REQ-FN-010 (ADR-029): Hardware and guided setup connects a ComfyUI that already runs, by its address.
// REQ-NFR-010 (ADR-030): Content policy "Keep log rows for N days". Runs against the test host only; no AppManager
// account is created, changed or used (the test host's /test/session opens a signed-in role).

async function openSession(page: Page, request: APIRequestContext, role: string) {
  const response = await request.post(`/test/session?role=${role}`);
  expect(response.ok()).toBeTruthy();
  const body = await response.json();
  await page.goto('/dev/session');
  await page.waitForLoadState('networkidle');
  const field = page.locator('#devtoken');
  await expect(async () => {
    await field.fill(body.token);
    await field.press('Tab');
    await expect(field).toHaveValue(body.token, { timeout: 2000 });
  }).toPass({ timeout: 30000 });
  await page.getByTestId('dev-establish').click();
  await page.waitForURL((url) => !url.pathname.startsWith('/dev/session'), { timeout: 30000 });
  return body.dataFolder as string;
}

async function noOverlapOrSideScroll(page: Page, cardTestId: string) {
  const sideScroll = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1);
  expect(sideScroll).toBeFalsy();
  const boxes = await page.getByTestId(cardTestId).locator('input, button, p, h3, [data-testid^="badge"]').evaluateAll((els) =>
    els.map((e) => { const r = (e as HTMLElement).getBoundingClientRect(); return { l: r.left, t: r.top, r: r.right, b: r.bottom, w: r.width, h: r.height, tag: e.tagName }; }).filter((x) => x.w > 0 && x.h > 0));
  const card = await page.getByTestId(cardTestId).boundingBox();
  for (const b of boxes) {
    expect(b.l).toBeGreaterThanOrEqual(card!.x - 1);
    expect(b.r).toBeLessThanOrEqual(card!.x + card!.width + 1);
  }
  const leaf = boxes.filter((x) => x.tag === 'INPUT' || x.tag === 'BUTTON');
  for (let i = 0; i < leaf.length; i++) for (let j = i + 1; j < leaf.length; j++) {
    const a = leaf[i], c = leaf[j];
    const overlap = a.l < c.r - 1 && c.l < a.r - 1 && a.t < c.b - 1 && c.t < a.b - 1;
    expect(overlap).toBeFalsy();
  }
}

test('REQ-FN-009 connecting a running ComfyUI shows its version, keeps the address, and a silent address keeps nothing', async ({ page, request }) => {
  const fake = http.createServer((req, res) => {
    if (req.url === '/system_stats') { res.setHeader('content-type', 'application/json'); res.end('{"system":{"comfyui_version":"0.3.44"},"devices":[]}'); }
    else { res.statusCode = 404; res.end(); }
  });
  await new Promise<void>((ok) => fake.listen(0, '127.0.0.1', () => ok()));
  const port = (fake.address() as any).port;
  const address = `http://127.0.0.1:${port}`;
  try {
    await openSession(page, request, 'Operator');
    await page.goto('/admin/setup/hardware');
    await expect(page.getByTestId('card-comfyui-address')).toBeVisible({ timeout: 30000 });
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(800);
    // REQ-FN-010: no Python step and no managed-ComfyUI step on the Mac
    await expect(page.locator('.setup-step', { hasText: 'Isolated Python' })).toHaveCount(0);
    await expect(page.locator('.setup-step', { hasText: 'Managed ComfyUI' })).toHaveCount(0);
    await expect(page.locator('.setup-step', { hasText: 'ONNX Runtime' })).toBeVisible();

    await page.getByTestId('input-comfyui-address').fill('http://127.0.0.1:9');
    await page.getByTestId('btn-connect-comfyui').click();
    await expect(page.getByTestId('text-comfyui-result')).toContainText('Nothing answered at http://127.0.0.1:9', { timeout: 20000 });
    await expect(page.getByTestId('badge-comfyui-connected')).toHaveText('Not connected');

    await page.getByTestId('input-comfyui-address').fill(address);
    await page.getByTestId('btn-connect-comfyui').click();
    await expect(page.getByTestId('text-comfyui-result')).toHaveText(`Connected to ComfyUI 0.3.44 at ${address}.`, { timeout: 20000 });
    await expect(page.getByTestId('badge-comfyui-connected')).toHaveText('Connected');
    await page.screenshot({ path: 'tests/.artifacts/verify/hardware-comfyui-desktop.png', fullPage: true });
    await noOverlapOrSideScroll(page, 'card-comfyui-address');

    // kept: a fresh page load shows the address and says it is connected
    await page.reload();
    await expect(page.getByTestId('text-comfyui-result')).toContainText('Connected to ComfyUI 0.3.44', { timeout: 30000 });
    await expect(page.getByTestId('input-comfyui-address')).toHaveValue(address);

    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/admin/setup/hardware');
    await expect(page.getByTestId('card-comfyui-address')).toBeVisible({ timeout: 30000 });
    await page.waitForTimeout(1500);
    await page.screenshot({ path: 'tests/.artifacts/verify/hardware-comfyui-mobile.png', fullPage: true });
    await noOverlapOrSideScroll(page, 'card-comfyui-address');
  } finally {
    fake.close();
  }
});

test('REQ-NFR-010 saving Keep log rows for N days removes old log rows and keeps every picture', async ({ page, request }) => {
  const dataFolder = await openSession(page, request, 'Owner');
  const db = path.join(dataFolder, 'TrStudio.db');
  const sql = (q: string) => execFileSync('sqlite3', [db, q]).toString().trim();
  // Ids unique per run: the fresh row is kept on purpose, so a second run on the same host must not reuse it.
  const run = Date.now().toString(36);
  sql(`INSERT INTO "GenerationLog" ("GenerationLogId","GenerationJobId","UserId","ModelId","Tier","Outcome","CreatedUtc") VALUES ('smoke-old-${run}','j','u','m','Low','Succeeded','2020-01-01T00:00:00.000Z'),('smoke-new-${run}','j','u','m','Low','Succeeded',strftime('%Y-%m-%dT%H:%M:%fZ','now'))`);
  sql(`INSERT INTO "GeneratedAsset" ("GeneratedAssetId","GenerationJobId","UserId","StoragePath","ModelId","CreatedUtc") VALUES ('smoke-asset-${run}','j','u','a.png','m','2020-01-01T00:00:00.000Z')`);

  await page.goto('/admin/policy');
  await expect(page.getByTestId('card-log-retention')).toBeVisible({ timeout: 30000 });
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(800);
  await page.screenshot({ path: 'tests/.artifacts/verify/policy-retention-desktop.png', fullPage: true });
  await noOverlapOrSideScroll(page, 'card-log-retention');

  await page.getByTestId('input-log-retention-days').fill('30');
  await page.getByTestId('btn-save-retention').click();
  await expect(page.getByTestId('text-retention-result')).toContainText(/Saved\. [1-9]\d* log rows older than 30 days were removed\./, { timeout: 20000 });
  expect(sql(`SELECT COUNT(*) FROM "GenerationLog" WHERE "GenerationLogId" IN ('smoke-old-${run}')`)).toBe('0');
  expect(sql(`SELECT COUNT(*) FROM "GenerationLog" WHERE "GenerationLogId" IN ('smoke-new-${run}')`)).toBe('1');
  expect(sql(`SELECT COUNT(*) FROM "GeneratedAsset" WHERE "GeneratedAssetId" = 'smoke-asset-${run}'`)).toBe('1');

  await page.reload();
  await expect(page.getByTestId('input-log-retention-days')).toHaveValue('30', { timeout: 30000 });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/admin/policy');
  await expect(page.getByTestId('card-log-retention')).toBeVisible({ timeout: 30000 });
  await page.waitForTimeout(1500);
  await page.screenshot({ path: 'tests/.artifacts/verify/policy-retention-mobile.png', fullPage: true });
  await noOverlapOrSideScroll(page, 'card-log-retention');

  // leave the default: keep for ever
  await page.getByTestId('input-log-retention-days').fill('0');
  await page.getByTestId('btn-save-retention').click();
  await expect(page.getByTestId('text-retention-result')).toContainText('Saved. Log rows are kept for ever.', { timeout: 20000 });
});
