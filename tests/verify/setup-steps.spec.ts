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

test('REQ-FN-010 provisioning Python on Hardware & setup is followed to its end on the screen', async ({ page, request }) => {
  test.setTimeout(240000);
  await openSession(page, request, 'Operator');
  await page.goto('/admin/setup');
  const row = page.locator('.setup-step', { hasText: 'Provision isolated Python' });
  await expect(row).toBeVisible({ timeout: 30000 });
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(500);

  await row.getByRole('button', { name: /Start|Verify/ }).click();

  // No Logs click: the row itself ends on the provisioned line (or would show the error).
  await expect(row).toContainText(/CPython [\d.]+ (already )?provisioned/, { timeout: 200000 });
  await expect(row.locator('.setup-stepdot')).toHaveClass(/done/);
  await expect(row.getByRole('button', { name: 'Verify' })).toBeVisible();
  await page.screenshot({ path: 'tests/.artifacts/verify/setup-python-done.png', fullPage: true });
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
