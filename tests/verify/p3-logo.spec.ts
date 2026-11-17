import { test, expect, Page } from '@playwright/test';

// The approved logo on Login (REQ-UI-108, owner 17 Nov 2026): the horizontal logo with the name in
// light mode; the tile and the white name in dark mode. Signed out, against the test host.

async function settle(page: Page) {
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(500);
}

test('REQ-UI-108 Login shows the approved logo in light and dark mode', async ({ page }) => {
  await page.goto('/login');
  await settle(page);
  const logo = page.getByTestId('img-trstudio-logo');
  await expect(logo).toBeVisible();
  await page.evaluate(() => document.documentElement.classList.remove('dark'));
  const light = logo.locator('img.brand-logo-light');
  await expect(light).toBeVisible();
  await expect(light).toHaveAttribute('src', /TrStudio-horizontal\.svg$/);
  expect(await light.evaluate((el: HTMLImageElement) => el.naturalWidth)).toBeGreaterThan(0);

  await page.evaluate(() => document.documentElement.classList.add('dark'));
  const word = logo.locator('img.brand-logo-word');
  await expect(word).toBeVisible();
  await expect(light).toBeHidden();
  expect(await word.evaluate((el: HTMLImageElement) => el.naturalWidth)).toBeGreaterThan(0);
});
