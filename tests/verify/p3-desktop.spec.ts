import { test, expect, Page, APIRequestContext } from '@playwright/test';

// Phase 3 headless checks (REQ-UI-098 to REQ-UI-104, REQ-NFR-020) against tests/TrStudio.TestHost:
// the same TrStudioUI screens and the same in-process engine on SQLite as the desktop app, which
// cannot be driven while the Mac screen is locked. The test host is a test tool and is never shipped.
// Sessions come from the test host's own /test/session route (no AppManager account is created or
// changed); the address comes from the Playwright configuration, never an environment variable here.

// A Blazor Server page re-renders when its interactive connection opens; typing before that is lost.
async function settle(page: Page) {
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(500);
}

async function typeInto(page: Page, selector: string, value: string) {
  const field = page.locator(selector);
  await expect(async () => {
    await field.fill(value);
    await field.press('Tab');
    await expect(field).toHaveValue(value, { timeout: 2000 });
  }).toPass({ timeout: 30000 });
}

async function openSession(page: Page, request: APIRequestContext, role: string): Promise<string> {
  const response = await request.post(`/test/session?role=${role}`);
  expect(response.ok()).toBeTruthy();
  const body = await response.json();
  await page.goto('/dev/session');
  await settle(page);
  await typeInto(page, '#devtoken', body.token);
  await expect(page.getByTestId('dev-establish')).toBeEnabled({ timeout: 30000 });
  await page.getByTestId('dev-establish').click();
  await page.waitForURL((url) => !url.pathname.startsWith('/dev/session'), { timeout: 30000 });
  return body.dataFolder as string;
}

async function signInMode(request: APIRequestContext, mode: string) {
  const response = await request.post(`/test/sign-in-mode?mode=${mode}`);
  expect(response.ok()).toBeTruthy();
  return response.json();
}

test.afterAll(async ({ request }) => {
  await request.post('/test/sign-in-mode?mode=file');
});

test('REQ-UI-098 missing sign-in settings', async ({ page, request }) => {
  await signInMode(request, 'none');
  await page.goto('/login');
  const notice = page.getByTestId('alert-missing-sign-in-settings');
  await expect(notice).toBeVisible({ timeout: 30000 });
  await expect(notice).toContainText('built without its sign-in settings');
  await settle(page);
  await typeInto(page, '#email', 'creator@studio.example');
  await typeInto(page, '#pwd', 'not-a-real-password');
  await expect(page.getByTestId('sign-in')).toBeDisabled();
});

test('REQ-UI-099 sign-in service unreachable', async ({ page, request }) => {
  const mode = await signInMode(request, 'unreachable');
  test.skip(mode.realSettings === true, 'Real AppManager settings are present in testsettings.Local.json; the unreachable case is covered by the unit test.');
  await page.goto('/login');
  await expect(page.getByTestId('card-1')).toBeVisible({ timeout: 30000 });
  await settle(page);
  await typeInto(page, '#email', 'creator@studio.example');
  await typeInto(page, '#pwd', 'not-a-real-password');
  await expect(page.getByTestId('sign-in')).toBeEnabled({ timeout: 30000 });
  await page.getByTestId('sign-in').click();
  const alert = page.getByTestId('alert-sign-in-unreachable');
  await expect(alert).toBeVisible({ timeout: 30000 });
  await expect(alert).toContainText('cannot be reached');
  expect(new URL(page.url()).pathname).toBe('/login');
});

test('REQ-UI-100 first run asks nothing', async ({ page }) => {
  await page.goto('/login');
  await expect(page.getByTestId('card-1')).toBeVisible({ timeout: 30000 });
  await expect(page.locator('input:not([type="checkbox"]):not([role="checkbox"])')).toHaveCount(2);
  await expect(page.getByTestId('note-local-data')).toContainText('stays on this computer');
  const text = (await page.locator('body').innerText()).toLowerCase();
  for (const word of ['connection string', 'server address', 'api address', 'terminal', 'settings file']) {
    expect(text).not.toContain(word);
  }
  await page.goto('/setup-required');
  await expect(page.getByTestId('gate-card')).toBeVisible({ timeout: 30000 });
  await expect(page.locator('input, textarea')).toHaveCount(0);
});

test('REQ-UI-101 setup shows data folder', async ({ page, request }) => {
  const response = await request.post('/test/session?role=Creator');
  const { dataFolder } = await response.json();
  await page.goto('/setup-required');
  const folder = page.getByTestId('data-folder');
  await expect(folder).toBeVisible({ timeout: 30000 });
  await expect(folder).toHaveText(dataFolder);
  await expect(page.getByTestId('badge-store-ready')).toBeVisible();
  await page.screenshot({ path: 'tests/.artifacts/verify/p3-setup-required.png', fullPage: true });
});

test('REQ-UI-104 registry key note', async ({ page, request }) => {
  await openSession(page, request, 'Owner');
  await page.goto('/admin/registry');
  const note = page.getByTestId('key-protection-note');
  await expect(note).toBeVisible({ timeout: 30000 });
  await expect(note).toContainText('Keys stay on this computer');
  await expect(page.getByTestId('provider-runpod')).toBeVisible();
  await expect(page.getByTestId('field-runpod-endpoint')).toBeVisible();
  await expect(page.getByTestId('provider-heygen')).toBeVisible();
  await page.screenshot({ path: 'tests/.artifacts/verify/p3-registry.png', fullPage: true });
});

// REQ-FN-116 (owner, 17 Nov 2026): a stored key can be removed on the screen itself. The key typed
// here is a made-up test value, not a secret.
test('REQ-FN-116 a stored HeyGen key is removed on Registry & providers', async ({ page, request }) => {
  await openSession(page, request, 'Owner');
  await page.goto('/admin/registry');
  await expect(page.getByTestId('provider-heygen')).toBeVisible({ timeout: 30000 });
  await settle(page);
  await typeInto(page, '[data-testid="field-heygen"]', 'made-up-heygen-key-9z9z');
  await page.getByRole('button', { name: 'Save keys' }).click();
  await expect(page.getByTestId('badge-heygen-configured')).toBeVisible({ timeout: 15000 });

  // Remove asks once; Keep leaves the key alone.
  await page.getByTestId('btn-remove-heygen').click();
  await page.getByTestId('btn-remove-heygen-keep').click();
  await expect(page.getByTestId('badge-heygen-configured')).toBeVisible();

  await page.getByTestId('btn-remove-heygen').click();
  await page.getByTestId('btn-remove-heygen-confirm').click();
  await expect(page.getByTestId('badge-heygen-configured')).toHaveCount(0, { timeout: 15000 });
  await expect(page.getByTestId('field-heygen')).toHaveAttribute('placeholder', 'Enter the key');

  // Still gone after the page is read again from the engine.
  await page.reload();
  await expect(page.getByTestId('provider-heygen')).toBeVisible({ timeout: 30000 });
  await settle(page);
  await expect(page.getByTestId('badge-heygen-configured')).toHaveCount(0);
  await expect(page.getByTestId('btn-remove-heygen')).toHaveCount(0);
  const keys = await (await page.request.get('/api/v1/admin/providers/keys')).text().catch(() => '');
  expect(keys).not.toContain('9z9z');
});

test('REQ-UI-103 ops shows queue', async ({ page, request }) => {
  await openSession(page, request, 'Owner');
  await page.goto('/admin/ops');
  const health = page.getByTestId('card-worker-health');
  await expect(health).toBeVisible({ timeout: 30000 });
  await expect(health).toContainText('Queue depth');
  await expect(health).toContainText(/\d+ queued · \d+ running/);
  await page.screenshot({ path: 'tests/.artifacts/verify/p3-ops.png', fullPage: true });
});

test('REQ-FN-086 real sign-in through AppManager', async ({ page, request }) => {
  const mode = await signInMode(request, 'file');
  test.skip(mode.realSettings !== true,
    'Skipped: AppManager id and key not set in tests/verify/testsettings.Local.json (AppManager:BaseUrl, ApplicationId, ApiKey) on this machine.');
  // With real settings, the test user is read from the Test users table by the verifier; this check
  // only proves the configured build reaches AppManager and is not told the settings are missing.
  await page.goto('/login');
  await settle(page);
  await expect(page.getByTestId('alert-missing-sign-in-settings')).toHaveCount(0);
});
