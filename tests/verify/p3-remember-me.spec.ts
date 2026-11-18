import { test, expect, Page, Browser, APIRequestContext } from '@playwright/test';

// "Remember me" (owner decision 6B, REQ-FN-100, REQ-UI-105, REQ-UI-106, REQ-FN-101) on tests/TrStudio.TestHost.
// The test host's stand-in AppManager (mode "fake", tests/TrStudio.TestHost/FakeAppManager.cs) answers
// sign-in, so no real AppManager account is created, changed or used. A new browser context is a new
// open of the app: it starts with no session of its own, as the desktop app does after it is closed.

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

async function standIn(request: APIRequestContext) {
  const response = await request.get('/test/appmanager');
  expect(response.ok()).toBeTruthy();
  return (await response.json()) as { logins: number; kept: boolean };
}

async function signIn(page: Page, rememberMe: boolean) {
  await page.goto('/login');
  await expect(page.getByTestId('card-1')).toBeVisible({ timeout: 30000 });
  await settle(page);
  await typeInto(page, '#email', 'owner@studio.example');
  await typeInto(page, '#pwd', 'test-only-password');
  if (rememberMe) {
    const box = page.getByTestId('remember-me');
    await box.click();
    await expect(box).toHaveAttribute('aria-checked', 'true', { timeout: 10000 });
  }
  await expect(page.getByTestId('sign-in')).toBeEnabled({ timeout: 30000 });
  await page.getByTestId('sign-in').click();
  await page.waitForURL((url) => !url.pathname.startsWith('/login'), { timeout: 30000 });
}

// A second open: a fresh browser context with no session, landing on Login as the app does on start.
async function openAgain(browser: Browser) {
  const context = await browser.newContext();
  const page = await context.newPage();
  await page.goto('/login');
  return { context, page };
}

async function stillAsksForSignIn(page: Page) {
  await expect(page.getByTestId('card-1')).toBeVisible({ timeout: 30000 });
  await settle(page);
  await page.waitForTimeout(1500);
  expect(new URL(page.url()).pathname).toBe('/login');
}

test.beforeAll(async ({ request }) => {
  const response = await request.post('/test/sign-in-mode?mode=fake');
  expect(response.ok()).toBeTruthy();
});

test.afterAll(async ({ request }) => {
  await request.post('/test/sign-in-mode?mode=file');
});

test('REQ-FN-100 ticked: a second open needs no sign-in', async ({ page, browser, request }) => {
  await signIn(page, true);
  const afterFirst = await standIn(request);
  expect(afterFirst.kept).toBe(true);

  const { context, page: second } = await openAgain(browser);
  await second.waitForURL((url) => !url.pathname.startsWith('/login'), { timeout: 30000 });
  await settle(second);
  await second.screenshot({ path: 'tests/.artifacts/verify/p3-remember-me-second-open.png', fullPage: true });
  const afterSecond = await standIn(request);
  expect(afterSecond.logins).toBe(afterFirst.logins);

  // Leave nothing kept for the next check.
  await second.getByTitle('Sign out').click();
  await second.waitForURL((url) => url.pathname.startsWith('/login'), { timeout: 30000 });
  expect((await standIn(request)).kept).toBe(false);
  await context.close();
});

test('REQ-UI-105 not ticked: a second open asks for sign-in', async ({ page, browser, request }) => {
  await signIn(page, false);
  expect((await standIn(request)).kept).toBe(false);

  const { context, page: second } = await openAgain(browser);
  await stillAsksForSignIn(second);
  await expect(second.getByTestId('remember-me')).toBeVisible();
  await second.screenshot({ path: 'tests/.artifacts/verify/p3-remember-me-login.png', fullPage: true });
  await context.close();
});

test('REQ-UI-106 signed out: a second open asks for sign-in', async ({ page, browser, request }) => {
  await signIn(page, true);
  expect((await standIn(request)).kept).toBe(true);

  await page.getByTitle('Sign out').click();
  await page.waitForURL((url) => url.pathname.startsWith('/login'), { timeout: 30000 });
  expect((await standIn(request)).kept).toBe(false);

  const { context, page: second } = await openAgain(browser);
  await stillAsksForSignIn(second);
  await context.close();
});

test('REQ-FN-101 refused kept sign-in: plain message and sign in again', async ({ page, browser, request }) => {
  await signIn(page, true);
  expect((await standIn(request)).kept).toBe(true);
  const revoke = await request.post('/test/appmanager/revoke');
  expect(revoke.ok()).toBeTruthy();

  const { context, page: second } = await openAgain(browser);
  const notice = second.getByTestId('alert-kept-sign-in-ended');
  await expect(notice).toBeVisible({ timeout: 30000 });
  await expect(notice).toContainText('sign in again');
  await settle(second);
  await second.waitForTimeout(1500);
  await expect(notice).toBeVisible();
  expect(new URL(second.url()).pathname).toBe('/login');
  expect((await standIn(request)).kept).toBe(false);
  await second.screenshot({ path: 'tests/.artifacts/verify/p3-remember-me-ended.png', fullPage: true });
  await context.close();
});
