import { test, expect, Page, APIRequestContext } from '@playwright/test';
import * as http from 'node:http';
import { AddressInfo } from 'node:net';

// Phase 2 (Venice) headless checks against tests/TrStudio.TestHost, the same TrStudioUI screens and
// in-process engine as the desktop app. No Venice key is used: a stand-in Venice runs inside this
// spec on 127.0.0.1 and answers like Venice's own API (model list, an auth refusal, a generated
// picture), and its address is typed into Registry & providers exactly as an admin would type
// Venice's. The key typed here is a made-up test value, not a secret. Each title starts with its row id.

const GoodKey = 'fake-venice-key-7Qx2';
const WrongKey = 'fake-venice-key-wrong';
const OnePixelPng = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==';

const ImageList = {
  data: [
    {
      id: 'fake-pic', type: 'image',
      model_spec: { name: 'Fake Pic', supportsStyleReferences: true, pricing: { generation: { usd: 0.01 } }, constraints: { aspectRatios: ['1:1'] } },
    },
  ],
};
const VideoList = {
  data: [
    {
      id: 'fake-reference-to-video', type: 'video',
      model_spec: { name: 'Fake R2V', constraints: { model_type: 'image-to-video', durations: ['5s', '10s'], resolutions: ['480p'], aspect_ratios: ['16:9'] } },
    },
    {
      id: 'fake-t2v', type: 'video',
      model_spec: { name: 'Fake T2V', constraints: { model_type: 'text-to-video', durations: ['5s'], resolutions: ['480p'], aspect_ratios: ['16:9'] } },
    },
  ],
};

let objServer: http.Server;
let objBaseUrl = '';
// REQ-FN-052: what Venice's /video/queue received, in order.
const objQueued: Record<string, unknown>[] = [];
// The smallest MP4 header the app accepts as a clip.
const TinyMp4 = Buffer.from([0, 0, 0, 24, 102, 116, 121, 112, 105, 115, 111, 109, 0, 0, 2, 0, 105, 115, 111, 109, 105, 115, 111, 50]);

function send(aResponse: http.ServerResponse, aStatus: number, aBody: unknown) {
  aResponse.writeHead(aStatus, { 'Content-Type': 'application/json' });
  aResponse.end(JSON.stringify(aBody));
}

test.beforeAll(async () => {
  objServer = http.createServer((aRequest, aResponse) => {
    const vUrl = new URL(aRequest.url ?? '/', 'http://127.0.0.1');
    const vAuth = aRequest.headers.authorization;
    // Venice answers a wrong key with 401 and its own text; Fill from Venice sends no key at all.
    if (vAuth && vAuth !== `Bearer ${GoodKey}`) {
      send(aResponse, 401, { error: 'Authentication failed' });
      return;
    }
    if (vUrl.pathname.endsWith('/models')) {
      const vType = vUrl.searchParams.get('type');
      send(aResponse, 200, vType === 'video' ? VideoList : vType === 'image' ? ImageList : { data: [] });
      return;
    }
    if (vUrl.pathname.endsWith('/image/generate')) {
      send(aResponse, 200, { id: 'fake-1', images: [OnePixelPng] });
      return;
    }
    if (vUrl.pathname.endsWith('/video/quote')) {
      send(aResponse, 200, { quote: 0.01 });
      return;
    }
    if (vUrl.pathname.endsWith('/video/queue')) {
      let vBody = '';
      aRequest.on('data', (aChunk) => { vBody += aChunk; });
      aRequest.on('end', () => {
        objQueued.push(JSON.parse(vBody || '{}'));
        send(aResponse, 200, { model: 'fake-reference-to-video', queue_id: `q-${objQueued.length}` });
      });
      return;
    }
    if (vUrl.pathname.endsWith('/video/retrieve')) {
      aResponse.writeHead(200, { 'Content-Type': 'video/mp4' });
      aResponse.end(TinyMp4);
      return;
    }
    if (vUrl.pathname.endsWith('/video/complete')) {
      send(aResponse, 200, { success: true });
      return;
    }
    send(aResponse, 404, { error: 'no fake route' });
  });
  await new Promise<void>((aResolve) => objServer.listen(0, '127.0.0.1', () => aResolve()));
  objBaseUrl = `http://127.0.0.1:${(objServer.address() as AddressInfo).port}/api/v1`;
});

test.afterAll(async () => {
  await new Promise<void>((aResolve) => objServer.close(() => aResolve()));
});

// A Blazor Server page re-renders when its interactive connection opens; typing before that is lost.
async function settle(aPage: Page) {
  await aPage.waitForLoadState('networkidle');
  await aPage.waitForTimeout(500);
}

async function typeInto(aPage: Page, aSelector: string, aValue: string) {
  const vField = aPage.locator(aSelector);
  await expect(async () => {
    await vField.fill(aValue);
    await vField.press('Tab');
    await expect(vField).toHaveValue(aValue, { timeout: 2000 });
  }).toPass({ timeout: 30000 });
}

async function openOwnerSession(aPage: Page, aRequest: APIRequestContext) {
  const vResponse = await aRequest.post('/test/session?role=Owner');
  expect(vResponse.ok()).toBeTruthy();
  const vBody = await vResponse.json();
  await aPage.goto('/dev/session');
  await settle(aPage);
  await typeInto(aPage, '#devtoken', vBody.token);
  await expect(aPage.getByTestId('dev-establish')).toBeEnabled({ timeout: 30000 });
  await aPage.getByTestId('dev-establish').click();
  await aPage.waitForURL((aUrl) => !aUrl.pathname.startsWith('/dev/session'), { timeout: 30000 });
}

async function openRegistry(aPage: Page) {
  await aPage.goto('/admin/registry');
  await expect(aPage.getByTestId('provider-venice')).toBeVisible({ timeout: 30000 });
  await settle(aPage);
}

// Types Venice's address and a key into the Venice card, switches it on and saves (no Venice call).
async function saveVenice(aPage: Page, aKey: string) {
  await openRegistry(aPage);
  await typeInto(aPage, '[data-testid=field-venice-address]', objBaseUrl);
  await typeInto(aPage, '[data-testid=field-venice-key]', aKey);
  const vSwitch = aPage.getByTestId('switch-venice');
  if ((await vSwitch.getAttribute('aria-checked')) !== 'true') {
    await vSwitch.click();
  }
  await aPage.getByRole('button', { name: 'Save keys' }).click();
  await settle(aPage);
}

async function noCircuitCrash(aPage: Page) {
  await expect(aPage.locator('#blazor-error-ui'), 'the page crashed (Blazor "An unhandled error has occurred")').toBeHidden();
}

async function pickOption(aPage: Page, aTrigger: string, aOption: string | RegExp) {
  await aPage.locator(aTrigger).locator('button, [role=combobox]').first().click();
  await aPage.getByRole('option', { name: aOption }).click();
}

// Adds a Venice registry row through the Add model sheet, filled from the stand-in Venice's model list.
async function addVeniceRow(aPage: Page, aProviderModelId: string, aSave: boolean) {
  await aPage.getByTestId('btn-add-model').click();
  await aPage.waitForTimeout(1000);
  // Seen 2026-11-14: opening the Add / Edit model sheet ends the page (Select at AdminRegistry.razor:262
  // carries data-testid, which TrBlazeUI's Select has no parameter for).
  await noCircuitCrash(aPage);
  await expect(aPage.getByTestId('venice-model-fields')).toBeVisible({ timeout: 15000 });
  await pickOption(aPage, '[data-testid=venice-model-fields] [data-slot=field]:first-child', 'Venice');
  await typeInto(aPage, '[data-testid=field-provider-model-id]', aProviderModelId);
  await aPage.getByTestId('btn-fill-from-venice').click();
  await expect(aPage.getByTestId('venice-fill-error')).toHaveCount(0);
  if (aSave) {
    await aPage.getByRole('button', { name: 'Save', exact: true }).click();
    await settle(aPage);
  }
}

test('REQ-FN-075 Venice address and key saved on Registry, key masked', async ({ page, request }) => {
  await openOwnerSession(page, request);
  await saveVenice(page, GoodKey);
  await noCircuitCrash(page);

  await page.reload();
  await openRegistry(page);
  await expect(page.getByTestId('field-venice-address')).toHaveValue(objBaseUrl);
  const vKey = page.getByTestId('field-venice-key');
  await expect(vKey).toHaveValue('');
  const vPlaceholder = (await vKey.getAttribute('placeholder')) ?? '';
  expect(vPlaceholder).toContain('7Qx2');
  expect(vPlaceholder).not.toContain(GoodKey);
  expect(await page.content()).not.toContain(GoodKey);
});

test('REQ-UI-083 Test connection shows Venice own error text, then Connected', async ({ page, request }) => {
  await openOwnerSession(page, request);
  await saveVenice(page, WrongKey);
  await page.getByTestId('btn-test-connection').click();
  await expect(page.getByTestId('venice-error')).toHaveText('Venice: Authentication failed (HTTP 401)', { timeout: 30000 });
  await expect(page.getByTestId('badge-connected')).toHaveCount(0);

  await typeInto(page, '[data-testid=field-venice-key]', GoodKey);
  await page.getByTestId('btn-test-connection').click();
  await expect(page.getByTestId('badge-connected')).toBeVisible({ timeout: 30000 });
  await expect(page.getByTestId('venice-error')).toHaveCount(0);
});

test('REQ-UI-097 Fill from Venice fills task, abilities, durations and sizes unsaved', async ({ page, request }) => {
  await openOwnerSession(page, request);
  await saveVenice(page, GoodKey);
  // Rows saved by earlier checks on the same test host stay in its database; count them first.
  const vSavedBefore = await page.getByTestId('table-1').getByRole('row').filter({ hasText: 'Fake R2V' }).count();
  await addVeniceRow(page, 'fake-reference-to-video', false);
  await expect(page.locator('#takes-reference-picture')).toBeChecked();
  await expect(page.locator('#keeps-fixed-character')).toBeChecked();
  await expect(page.getByTestId('param-values').filter({ hasText: 'duration' })).toContainText('5s, 10s');
  await expect(page.getByTestId('param-values').filter({ hasText: 'resolution' })).toContainText('480p');
  await expect(page.getByTestId('param-values').filter({ hasText: 'aspectRatio' })).toContainText('16:9');
  // Unsaved: the table has no more such rows than before the fill.
  await expect(page.getByTestId('table-1').getByRole('row').filter({ hasText: 'Fake R2V' })).toHaveCount(vSavedBefore);
});

test('REQ-UI-084 a saved Venice model row lists with a Venice badge', async ({ page, request }) => {
  await openOwnerSession(page, request);
  await saveVenice(page, GoodKey);
  await addVeniceRow(page, 'fake-reference-to-video', true);
  const vRow = page.getByTestId('table-1').getByRole('row').filter({ hasText: 'Fake R2V' });
  await expect(vRow).toHaveCount(1, { timeout: 15000 });
  await expect(vRow.locator('[data-testid^="badge-venice"]')).toBeVisible();
});

test('REQ-FN-077 reference picture and fixed character ticks remain after reload', async ({ page, request }) => {
  await openOwnerSession(page, request);
  await saveVenice(page, GoodKey);
  await addVeniceRow(page, 'fake-reference-to-video', true);
  await page.reload();
  await openRegistry(page);
  const vRow = page.getByTestId('table-1').getByRole('row').filter({ hasText: 'Fake R2V' });
  await vRow.getByRole('button', { name: 'Edit model' }).click();
  await noCircuitCrash(page);
  await expect(page.locator('#takes-reference-picture')).toBeChecked({ timeout: 15000 });
  await expect(page.locator('#keeps-fixed-character')).toBeChecked();
});

test('REQ-UI-085 Image studio lists an enabled Venice picture row badged Cloud · Venice', async ({ page, request }) => {
  await openOwnerSession(page, request);
  await saveVenice(page, GoodKey);
  await addVeniceRow(page, 'fake-pic', true);
  await page.goto('/image');
  await expect(page.getByTestId('image-studio')).toBeVisible({ timeout: 30000 });
  await settle(page);
  // The model list is a native select (as the mockup draws it): its options are listed, not shown.
  await expect(page.getByTestId('field-1').locator('option', { hasText: 'Fake Pic — Cloud · Venice' })).toHaveCount(1, { timeout: 15000 });
});

test('REQ-UI-096 picking a Venice model shows the sent-to-Venice notice', async ({ page, request }) => {
  await openOwnerSession(page, request);
  await saveVenice(page, GoodKey);
  await addVeniceRow(page, 'fake-pic', true);
  await page.goto('/image');
  await expect(page.getByTestId('image-studio')).toBeVisible({ timeout: 30000 });
  await settle(page);
  await expect(page.getByTestId('field-1').locator('option', { hasText: 'Fake Pic — Cloud · Venice' })).toHaveCount(1, { timeout: 15000 });
  await page.getByTestId('field-1').selectOption({ label: 'Fake Pic — Cloud · Venice' });
  await expect(page.getByTestId('alert-sent-to-venice')).toContainText('Your prompt and pictures are sent to Venice.');
  await expect(page.getByTestId('badge-cloud-venice')).toBeVisible();
});

test('REQ-UI-086 generating with a Venice model shows Venice picture in a result tile', async ({ page, request }) => {
  await openOwnerSession(page, request);
  await saveVenice(page, GoodKey);
  await addVeniceRow(page, 'fake-pic', true);
  await page.goto('/image');
  await expect(page.getByTestId('image-studio')).toBeVisible({ timeout: 30000 });
  await settle(page);
  await expect(page.getByTestId('field-1').locator('option', { hasText: 'Fake Pic — Cloud · Venice' })).toHaveCount(1, { timeout: 15000 });
  await page.getByTestId('field-1').selectOption({ label: 'Fake Pic — Cloud · Venice' });
  const vPrompt = page.getByTestId('field-prompt');
  await vPrompt.fill('a small red apple');
  await vPrompt.press('Tab');
  await page.getByTestId('btn-generate').click();
  const vTile = page.getByTestId('result-tile').filter({ has: page.getByTestId('badge-result-venice') });
  await expect(vTile.first()).toBeVisible({ timeout: 60000 });
  await expect(vTile.first().getByTestId('result-picture')).toBeVisible();
});

test('REQ-UI-089 Text to video lists Venice text-to-video rows badged', async ({ page, request }) => {
  await openOwnerSession(page, request);
  await saveVenice(page, GoodKey);
  await addVeniceRow(page, 'fake-t2v', true);
  await page.goto('/video');
  await expect(page.getByTestId('video-studio')).toBeVisible({ timeout: 30000 });
  await settle(page);
  await page.getByTestId('btn-text-video').click();
  const vModels = page.getByTestId('field-video-model');
  await expect(vModels.locator('option', { hasText: 'Fake T2V — Cloud · Venice' })).toHaveCount(1, { timeout: 15000 });
  await expect(vModels.locator('option', { hasText: /Fake R2V/ })).toHaveCount(0);
});

test('REQ-UI-091 Image to video lists only image-to-video rows', async ({ page, request }) => {
  await openOwnerSession(page, request);
  await saveVenice(page, GoodKey);
  await addVeniceRow(page, 'fake-reference-to-video', true);
  await openRegistry(page);
  await addVeniceRow(page, 'fake-t2v', true);
  await page.goto('/video');
  await expect(page.getByTestId('video-studio')).toBeVisible({ timeout: 30000 });
  await settle(page);
  await page.getByTestId('btn-text-video').click();
  await page.getByTestId('btn-image-video').click();
  const vModels = page.getByTestId('field-video-model');
  await expect(vModels.locator('option', { hasText: 'Fake R2V — Cloud · Venice' })).toHaveCount(1, { timeout: 15000 });
  await expect(vModels.locator('option', { hasText: /Fake T2V/ })).toHaveCount(0);
});

// REQ-FN-052 (owner run of 17 Nov 2026): the frames pinned on Video studio reach the model. Before, only
// their file names were sent and Venice got no picture at all.
const RedPng = 'iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAFklEQVR4nGP8z8DAwMDAxMDAwMDAAAANHQEDasKb6QAAAABJRU5ErkJggg==';
test('REQ-FN-052 pinned first and last frames reach Venice as the start and end pictures', async ({ page, request }) => {
  await openOwnerSession(page, request);
  await saveVenice(page, GoodKey);
  await addVeniceRow(page, 'fake-reference-to-video', true);
  await page.goto('/video');
  await expect(page.getByTestId('video-studio')).toBeVisible({ timeout: 30000 });
  await settle(page);
  const vFrames = page.getByTestId('card-conditioning-frames').locator('input[type=file]');
  await vFrames.nth(0).setInputFiles({ name: 'start.png', mimeType: 'image/png', buffer: Buffer.from(OnePixelPng, 'base64') });
  await vFrames.nth(1).setInputFiles({ name: 'end.png', mimeType: 'image/png', buffer: Buffer.from(RedPng, 'base64') });
  await expect(page.getByTestId('card-conditioning-frames')).toContainText('start.png');
  await expect(page.getByTestId('card-conditioning-frames')).toContainText('end.png');
  const vModels = page.getByTestId('field-video-model');
  await vModels.selectOption({ label: 'Fake R2V — Cloud · Venice' });
  const vBefore = objQueued.length;
  await page.getByRole('button', { name: 'Generate from frame' }).click();
  await expect.poll(() => objQueued.length, { timeout: 30000 }).toBeGreaterThan(vBefore);
  const vSent = objQueued[objQueued.length - 1];
  expect(String(vSent['image_url'])).toBe(`data:image/png;base64,${OnePixelPng}`);
  expect(String(vSent['end_image_url'])).toBe(`data:image/png;base64,${RedPng}`);
});
