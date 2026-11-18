import { test, expect, Page, APIRequestContext } from '@playwright/test';

// Owner screen check, 17 Nov 2026: Video studio's Model box cut a long seeded model name at 1280 px
// ("Veo 3.1 Fast image to video — Cloud · Venice"). A closed native select shows one line and clips
// what does not fit, so this check measures the chosen option's text in the box's own font against
// the room the box gives it (its inner width less padding and the arrow), for every listed model.
// Sample data: tests/verify/seed/video-studio.sh (POST /test/seed/video-studio, stand-in user 4242).

async function openOwnerSession(aPage: Page, aRequest: APIRequestContext) {
  const vResponse = await aRequest.post('/test/session?role=Owner');
  expect(vResponse.ok()).toBeTruthy();
  const vBody = await vResponse.json();
  await aPage.goto('/dev/session');
  await aPage.waitForLoadState('networkidle');
  const vField = aPage.locator('#devtoken');
  await expect(async () => {
    await vField.fill(vBody.token);
    await vField.press('Tab');
    await expect(vField).toHaveValue(vBody.token, { timeout: 2000 });
  }).toPass({ timeout: 30000 });
  await expect(aPage.getByTestId('dev-establish')).toBeEnabled({ timeout: 30000 });
  await aPage.getByTestId('dev-establish').click();
  await aPage.waitForURL((aUrl) => !aUrl.pathname.startsWith('/dev/session'), { timeout: 30000 });
}

/** For each option of the select: its text, the pixels it needs, and the pixels the closed box gives. */
async function fitOfEveryOption(aPage: Page, aTestId: string) {
  return aPage.getByTestId(aTestId).evaluate((aSelect: HTMLSelectElement) => {
    const vStyle = getComputedStyle(aSelect);
    const vCanvas = document.createElement('canvas').getContext('2d')!;
    vCanvas.font = `${vStyle.fontStyle} ${vStyle.fontWeight} ${vStyle.fontSize} ${vStyle.fontFamily}`;
    // The arrow sits inside the right padding on these selects; the text gets the content box only.
    const vRoom = aSelect.clientWidth - parseFloat(vStyle.paddingLeft) - parseFloat(vStyle.paddingRight);
    return Array.from(aSelect.options).map((aOption) => ({
      text: aOption.text,
      needs: Math.ceil(vCanvas.measureText(aOption.text).width),
      room: Math.floor(vRoom),
    }));
  });
}

for (const vWidth of [1280, 390]) {
  test(`REQ-UI-040 Video studio Model box shows every seeded model name whole @${vWidth}`, async ({ page, request }) => {
    await page.setViewportSize({ width: vWidth, height: 900 });
    expect((await request.post('/test/seed/video-studio')).ok()).toBeTruthy();
    await openOwnerSession(page, request);
    await page.goto('/video');
    await expect(page.getByTestId('video-studio')).toBeVisible({ timeout: 30000 });
    await page.waitForLoadState('networkidle');
    const vModels = page.getByTestId('field-video-model');
    await expect(vModels.locator('option', { hasText: 'Veo 3.1 Fast image to video' })).toHaveCount(1, { timeout: 15000 });

    const vFit = await fitOfEveryOption(page, 'field-video-model');
    expect(vFit.length).toBeGreaterThanOrEqual(4);
    // Before the fix the closed select itself drew the text, so a long name was cut. Now the select is
    // invisible and lies exactly over a label that wraps; every model, picked in turn, is shown whole.
    const vSelect = await vModels.boundingBox();
    const vChosen = page.getByTestId('video-model-chosen');
    expect(await vModels.evaluate((aSelect) => getComputedStyle(aSelect).opacity)).toBe('0');
    for (const vRow of vFit) {
      await vModels.selectOption({ label: vRow.text });
      await expect(vChosen).toHaveText(vRow.text);
      const vShown = await vChosen.evaluate((aLabel) => ({
        clipped: aLabel.scrollWidth > aLabel.clientWidth || aLabel.scrollHeight > aLabel.clientHeight,
        width: aLabel.getBoundingClientRect().width,
      }));
      expect(vShown.clipped, `"${vRow.text}" is cut in the Model box`).toBe(false);
      const vBox = await vModels.boundingBox();
      const vLabel = await vChosen.boundingBox();
      // the invisible select covers the whole drawn box, so a click anywhere on it opens the list
      expect(Math.abs(vBox!.height - vLabel!.height)).toBeLessThanOrEqual(1);
      expect(Math.abs(vBox!.width - vLabel!.width)).toBeLessThanOrEqual(1);
      expect(vSelect!.width).toBeGreaterThan(0);
    }

    // The provider is still said beside the box: picking a Venice row shows its badge.
    await vModels.selectOption({ label: (vFit.find((aRow) => aRow.text.startsWith('Veo 3.1 Fast')))!.text });
    await expect(page.getByTestId('badge-cloud-venice')).toBeVisible();
    await page.getByTestId('video-model-picker').screenshot({ path: `tests/.artifacts/verify/video-model-box-${vWidth}.png` });
  });
}

// Screen check of 18 Nov 2026: Comparison's Slot A and Slot B cut a long seeded asset name at 390 px.
// The chosen name is drawn in a label that wraps, under the invisible native select.
for (const vWidth of [1280, 390]) {
  test(`REQ-UI-052 Comparison slot boxes show every seeded asset name whole @${vWidth}`, async ({ page, request }) => {
    await page.setViewportSize({ width: vWidth, height: 900 });
    expect((await request.post('/test/seed/compare')).ok()).toBeTruthy();
    // The compare seed belongs to the stand-in AppManager's user 4242: sign in as that user through Login.
    expect((await request.post('/test/sign-in-mode?mode=fake')).ok()).toBeTruthy();
    try {
      await page.goto('/login');
      await page.waitForLoadState('networkidle');
      const vEmail = page.locator('input[type=email]');
      await expect(async () => {
        await vEmail.fill('owner@trstudio.test');
        await page.locator('input[type=password]').fill('made-up-pass-1');
        await expect(page.getByTestId('sign-in')).toBeEnabled({ timeout: 2000 });
      }).toPass({ timeout: 30000 });
      await page.getByTestId('sign-in').click();
      await page.waitForURL((aUrl) => !aUrl.pathname.startsWith('/login'), { timeout: 30000 });
    } finally {
      await request.post('/test/sign-in-mode?mode=file');
    }
    await page.goto('/compare');
    await expect(page.getByTestId('compare-slotbar')).toBeVisible({ timeout: 30000 });
    await page.waitForLoadState('networkidle');
    for (const vSlot of [1, 2]) {
      const vSelect = page.getByTestId(`field-${vSlot}`);
      const vChosen = page.getByTestId(`slot-${vSlot}-chosen`);
      const vLabels = await vSelect.evaluate((aSelect: HTMLSelectElement) => Array.from(aSelect.options).filter((aOption) => aOption.value).map((aOption) => aOption.text));
      expect(vLabels.length).toBeGreaterThan(0);
      for (const vLabel of vLabels) {
        await vSelect.selectOption({ label: vLabel });
        await expect(vChosen).toHaveText(vLabel);
        const vClipped = await vChosen.evaluate((aLabel) => aLabel.scrollWidth > aLabel.clientWidth || aLabel.scrollHeight > aLabel.clientHeight);
        expect(vClipped, `"${vLabel}" is cut in slot ${vSlot}`).toBe(false);
        const vBox = await vSelect.boundingBox();
        const vDrawn = await vChosen.boundingBox();
        expect(Math.abs(vBox!.width - vDrawn!.width)).toBeLessThanOrEqual(1);
        expect(Math.abs(vBox!.height - vDrawn!.height)).toBeLessThanOrEqual(1);
      }
    }
    await page.getByTestId('compare-slotbar').screenshot({ path: `tests/.artifacts/verify/compare-slots-${vWidth}.png` });
  });
}
