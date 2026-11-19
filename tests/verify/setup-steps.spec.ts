import { test, expect, Page, APIRequestContext } from '@playwright/test';

// REQ-FN-010 on Hardware & guided setup (owner run of 17 Nov 2026): the Operator provisions the managed
// Python environment from the screen, and the row follows the step to its end — before, it stayed at
// "0 %" and a failure was seen only after pressing Logs. On the test host the managed root is inside its
// temporary data folder, so the real download (python-build-standalone, about 30 MB) lands only there.

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
}

// Changed 19 Nov 2026 (owner decision 17B, ADR-029): on Mac the App Sandbox stays, so Hardware & setup
// offers no Python step and no managed ComfyUI download; ComfyUI is connected by its address instead.
// The test host runs on this Mac, so it shows the Mac steps. (The provisioning flow above is the Windows
// head's, a later version.)
test('REQ-FN-010 Hardware & setup on Mac offers no Python step and connecting ComfyUI starts no program', async ({ page, request }) => {
  await openSession(page, request, 'Operator');
  await page.goto('/admin/setup');
  await expect(page.getByTestId('card-comfyui-address')).toBeVisible({ timeout: 30000 });
  await page.waitForLoadState('networkidle');
  await expect(page.locator('.setup-step', { hasText: /Python/ })).toHaveCount(0);
  await expect(page.locator('.setup-step', { hasText: /Provision.*ComfyUI|managed ComfyUI/i })).toHaveCount(0);

  // A closed port: Connect makes one HTTP call, reports it plainly, keeps nothing and starts nothing.
  await page.getByTestId('input-comfyui-address').fill('http://127.0.0.1:9');
  await page.getByTestId('btn-connect-comfyui').click();
  await expect(page.getByTestId('text-comfyui-result')).toContainText(/Nothing answered/, { timeout: 20000 });
  await expect(page.getByTestId('badge-comfyui-connected')).toContainText('Not connected');
  await expect(page.locator('.setup-step', { hasText: /Python/ })).toHaveCount(0);
  await page.screenshot({ path: 'tests/.artifacts/verify/setup-mac-no-python.png', fullPage: true });
});

// REQ-NFR-006: a job that would go to a closed API is flagged on the screen before it is sent; a local
// (Fast) job shows no such banner.
test('REQ-NFR-006 closed-API tier shows the routing banner, the local tier does not', async ({ page, request }) => {
  await openSession(page, request, 'Creator');
  await page.goto('/video');
  await expect(page.getByTestId('video-studio')).toBeVisible({ timeout: 30000 });
  await page.waitForLoadState('networkidle');
  const tiers = page.getByTestId('tier-group');
  await tiers.getByRole('radio', { name: /Fast/ }).click();
  await expect(page.getByTestId('card-quality-tier').getByTestId('alert-info')).toHaveCount(0);
  await tiers.getByRole('radio', { name: /Premium/ }).click();
  const banner = page.getByTestId('card-quality-tier').getByTestId('alert-info');
  await expect(banner).toBeVisible();
  await expect(banner).toContainText('closed API');
});
