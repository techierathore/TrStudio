import { test, expect, Page, APIRequestContext } from '@playwright/test';

// Owner run of 15 Nov 2026, part A, against tests/TrStudio.TestHost (the same screens and engine as
// the Mac app, headless; the test host is the stand-in service, never shipped):
// - ComfyUI import "Save as model" really writes a registry model (fixed 15 Nov 2026, no check until now).
// - ComfyUI import Run says plainly that the debug service was not reached, with no made-up node results.
// - Every screen that needs no sign-in is reached from Login by the clicks the Mac screen checker
//   reads from the code (tf-verify-list.py): "Sign in", then the screen's own link.
// The address comes from the Playwright configuration; sessions come from the test host's /test/session.

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

async function openSession(page: Page, request: APIRequestContext, role: string) {
  const response = await request.post(`/test/session?role=${role}`);
  expect(response.ok()).toBeTruthy();
  const body = await response.json();
  await page.goto('/dev/session');
  await settle(page);
  await typeInto(page, '#devtoken', body.token);
  await expect(page.getByTestId('dev-establish')).toBeEnabled({ timeout: 30000 });
  await page.getByTestId('dev-establish').click();
  await page.waitForURL((url) => !url.pathname.startsWith('/dev/session'), { timeout: 30000 });
}

// Base ComfyUI nodes only, so the dependency scan finds nothing missing and the graph is known-good.
const BASE_GRAPH = JSON.stringify({
  '3': { class_type: 'KSampler', inputs: { seed: -1, steps: 30, cfg: 7.0, sampler_name: 'euler', denoise: 1.0 } },
  '4': { class_type: 'CheckpointLoaderSimple', inputs: { ckpt_name: 'sd_xl_base_1.0.safetensors' } },
  '6': { class_type: 'CLIPTextEncode', inputs: { text: 'a cinematic studio portrait' } },
  '8': { class_type: 'VAEDecode', inputs: { samples: ['3', 0] } },
  '9': { class_type: 'SaveImage', inputs: { images: ['8', 0] } },
}, null, 1);

// Built-in nodes and no model file: the engine's dependency scan cannot ask a ComfyUI which model files
// it holds, so a graph naming one never reads known-good from a fresh import (logged on REQ-FN-047).
const SAVE_GRAPH = JSON.stringify({
  '3': { class_type: 'KSampler', inputs: { seed: -1, steps: 20, cfg: 7.0, sampler_name: 'euler', denoise: 1.0 } },
  '5': { class_type: 'EmptyLatentImage', inputs: { width: 1024, height: 1024, batch_size: 1 } },
  '6': { class_type: 'CLIPTextEncode', inputs: { text: 'a cinematic studio portrait' } },
  '8': { class_type: 'VAEDecode', inputs: { samples: ['3', 0] } },
  '9': { class_type: 'SaveImage', inputs: { images: ['8', 0] } },
}, null, 1);

async function openComfyWith(page: Page, request: APIRequestContext, graph: string) {
  await openSession(page, request, 'Operator');
  await page.goto('/admin/comfy/import');
  await settle(page);
  await typeInto(page, '[data-testid="field-1"] textarea, textarea[data-testid="field-1"]', graph);
  await page.getByTestId('btn-parse-workflow').click();
  await expect(page.getByTestId('btn-run')).toBeEnabled({ timeout: 30000 });
}

test.afterEach(async ({ request }) => {
  await request.post('/test/outage');   // clear any outage a test set
});

test('REQ-FN-048 ComfyUI Save as model writes a registry model', async ({ page, request }) => {
  await openComfyWith(page, request, SAVE_GRAPH);
  await page.getByTestId('btn-run').click();
  await expect(page.getByTestId('badge-known-good')).toBeVisible({ timeout: 30000 });

  const name = `Portrait check ${Date.now()}`;
  const modelId = name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
  const before = await (await request.get(`/test/registry-model?id=${modelId}`)).json();
  expect(before.exists).toBe(false);

  await typeInto(page, '[data-testid="field-model-name-e-g-sdxl-port"]', name);
  await expect(page.getByTestId('btn-save-as-model')).toBeEnabled({ timeout: 30000 });
  await page.getByTestId('btn-save-as-model').click();
  await expect(page.getByText('Registry model saved')).toBeVisible({ timeout: 30000 });

  await expect(async () => {
    const after = await (await request.get(`/test/registry-model?id=${modelId}`)).json();
    expect(after.exists).toBe(true);
    expect(after.displayName).toBe(name);
  }).toPass({ timeout: 15000 });
});

test('REQ-FN-046 ComfyUI Run says the debug service was not reached and shows no made-up results', async ({ page, request }) => {
  await openComfyWith(page, request, BASE_GRAPH);
  const outage = await request.post('/test/outage?path=/api/v1/admin/comfy/debug');
  expect(outage.ok()).toBeTruthy();

  await page.getByTestId('btn-run').click();

  const notice = page.getByTestId('alert-run-not-reached');
  await expect(notice).toBeVisible({ timeout: 30000 });
  await expect(notice).toContainText('was not reached');
  const panel = page.getByTestId('card-run-debug');
  await expect(panel).not.toContainText('Completed');
  await expect(panel).not.toContainText('Stopped at');
  await expect(panel.locator('.comfy-nstat')).toHaveCount(0);
  await expect(page.getByTestId('badge-known-good')).toHaveCount(0);
  await expect(page.getByTestId('btn-save-as-model')).toBeDisabled();
});

// The order the Mac checker visits them (tf-verify-list.sh): it starts on Login, the screen the app
// opens on, and clicks each path from wherever the last one left it, as a person would.
const AUTH_PATHS: { name: string; path: string; nav: string[]; anchor: string }[] = [
  { name: 'Register', path: '/register', nav: ['Sign in', 'Create an account'], anchor: 'btn-creator' },
  { name: 'Login', path: '/login', nav: ['Sign in'], anchor: 'remember-me-row' },
  { name: 'Forgot password', path: '/forgot-password', nav: ['Sign in', 'Forgot password'], anchor: 'btn-send-reset-link' },
  { name: 'Setup required', path: '/setup-required', nav: ['Sign in', 'See the setup gate'], anchor: 'badge-status' },
];

test('REQ-UI-001 REQ-UI-002 REQ-UI-003 REQ-UI-004 every screen without sign-in is reached from Login by its links', async ({ page }) => {
  await page.goto('/login');
  await settle(page);
  for (const screen of AUTH_PATHS) {
    for (const label of screen.nav) {
      // the checker's rule: a button or link labelled exactly so, else one whose label holds it
      const exact = page.locator('a, button').filter({ hasText: new RegExp(`^\\s*${label}\\s*$`, 'i') });
      const target = (await exact.count()) ? exact.first() : page.locator('a, button').filter({ hasText: new RegExp(label, 'i') }).first();
      await expect(target, `"${label}" on the way to ${screen.name}`).toHaveCount(1);
      await target.click({ force: true });   // the checker clicks blind; a disabled "Sign in" does nothing
      await page.waitForTimeout(400);
    }
    await expect(page, `${screen.name} reached`).toHaveURL(new RegExp(`${screen.path}$`), { timeout: 15000 });
    await expect(page.getByTestId(screen.anchor)).toBeVisible({ timeout: 15000 });
  }
});
