import { test, expect, Page, APIRequestContext } from '@playwright/test';

// REQ-FN-104 (owner run 16 Nov 2026, real Mac app): the Mac app zooms the page with CSS zoom (1.6 by
// default on the owner's screen) and sets --tr-zoom to the same level (src/TrStudio/BlazorZoom.cs).
// CSS zoom also scales a 100vh box, so the signed-in shell's sidebar drew 1.6 windows tall and its
// footer (Sign out) sat below the window, out of reach. This applies the same zoom on the test host's
// signed-in shell and checks the sidebar footer stays inside the window.

async function settle(page: Page) {
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(500);
}

async function openSession(page: Page, request: APIRequestContext, role: string) {
  const response = await request.post(`/test/session?role=${role}`);
  expect(response.ok()).toBeTruthy();
  const body = await response.json();
  await page.goto('/dev/session');
  await settle(page);
  const field = page.locator('#devtoken');
  await expect(async () => {
    await field.fill(body.token);
    await field.press('Tab');
    await expect(field).toHaveValue(body.token, { timeout: 2000 });
  }).toPass({ timeout: 30000 });
  await expect(page.getByTestId('dev-establish')).toBeEnabled({ timeout: 30000 });
  await page.getByTestId('dev-establish').click();
  await page.waitForURL((url) => !url.pathname.startsWith('/dev/session'), { timeout: 30000 });
}

test('REQ-FN-104 at the Mac zoom the sidebar footer (Sign out) stays inside the window', async ({ page, request }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await openSession(page, request, 'Owner');
  await page.goto('/admin/setup/hardware');
  await settle(page);
  // what BlazorZoom's script puts on the page at the default level
  await page.evaluate(() => {
    const d = document.documentElement;
    d.style.setProperty('zoom', '1.6');
    d.style.setProperty('--tr-zoom', '1.6');
  });
  await page.waitForTimeout(500);
  const signOut = page.getByTestId('nav-sign-out');
  await expect(signOut).toBeVisible();
  // the box Playwright reports is in window pixels, as drawn
  const box = await signOut.boundingBox();
  expect(box, 'Sign out has no box on screen').not.toBeNull();
  expect(box!.y + box!.height, `Sign out ends at ${Math.round(box!.y + box!.height)} px in an 800 px window`).toBeLessThanOrEqual(800);
});

// REQ-FN-104 (owner run 16 Nov 2026, real Mac app, Avatar studio @1280): CSS zoom does not change what
// a width media query measures, so at 1.6 the three studio columns never folded and the right column
// (Render tier) covered the script. The studio columns must not overlap at the Mac zoom.
for (const zoom of ['1.25', '1.6']) for (const route of ['/avatar', '/image', '/video']) {
  test(`REQ-FN-104 at zoom ${zoom} the studio columns on ${route} do not overlap`, async ({ page, request }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await openSession(page, request, 'Owner');
    await page.goto(route);
    await settle(page);
    await page.evaluate((z) => {
      const d = document.documentElement;
      d.style.setProperty('zoom', z);
      d.style.setProperty('--tr-zoom', z);
    }, zoom);
    await page.waitForTimeout(500);
    const fit = await page.locator('.studio').evaluate((st) => {
      const widths = Array.from(st.children).map((c) => Math.round((c as HTMLElement).getBoundingClientRect().width));
      return { widths, scroll: st.scrollWidth, client: st.clientWidth };
    });
    // WebKit (the Mac app) drew the columns over each other; Chromium squeezes the middle one to 0 px
    // and pushes the right one past the window. Either way: every column readable, nothing sticking out.
    for (const w of fit.widths) expect(w, `a studio column is ${w} px wide (${fit.widths.join(', ')})`).toBeGreaterThanOrEqual(120);
    expect(fit.scroll, `the studio is ${fit.scroll} px wide inside ${fit.client} px`).toBeLessThanOrEqual(fit.client + 1);
  });
}

// Owner run 16 Nov 2026, real Mac app: after the window was narrowed to phone width and widened again,
// the sidebar stayed folded behind the top bar's toggle, and the menu sheet stayed open over the page
// after choosing a screen. Back at desktop width the desktop sidebar must show, with no sheet over the page.
test('REQ-FN-104 the desktop sidebar comes back after a phone-width window', async ({ page, request }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await openSession(page, request, 'Owner');
  await page.goto('/gallery');
  await settle(page);
  await expect(page.getByTestId('nav-sign-out')).toBeVisible();
  await page.setViewportSize({ width: 390, height: 844 });
  await page.waitForTimeout(1200);
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.waitForTimeout(1200);
  await expect(page.getByTestId('nav-sign-out'), 'the desktop sidebar is back at 1280').toBeVisible();
  await expect(page.locator('[role="dialog"]'), 'no menu sheet over the page at 1280').toHaveCount(0);
});

test('REQ-FN-104 choosing a screen from the phone menu closes the menu', async ({ page, request }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await openSession(page, request, 'Owner');
  await page.goto('/gallery');
  await settle(page);
  await page.getByRole('button', { name: /toggle sidebar/i }).first().click();
  await page.waitForTimeout(600);
  await page.getByRole('link', { name: 'Library' }).first().click();
  await page.waitForURL(/\/library$/);
  await page.waitForTimeout(800);
  await expect(page.locator('[role="dialog"]'), 'the menu sheet closes after choosing a screen').toHaveCount(0);
});
