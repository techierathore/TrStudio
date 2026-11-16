// Real Mac app: opens, by clicking the menu a person sees, the signed-in screens the Mac screen checker
// cannot reach (TF-006), and screenshots each at 1280 and 390 wide. Owner run of 15 Nov 2026, part B.
//   node tests/verify/mac-sweep.mjs
import { mkdirSync, readFileSync } from 'node:fs';
import { openSession, parseSource, appPid, resizeWindow, windowShot } from '../../.tfcore/utils/tf-appium.mjs';

const BOOT = JSON.parse(readFileSync('tests/.artifacts/verify/boot-4723.json', 'utf8'));
const OUT = 'tests/.artifacts/verify/mac/sweep';
mkdirSync(OUT, { recursive: true });
const sleep = (ms) => new Promise((ok) => setTimeout(ok, ms));
const q = (t) => t.replace(/'/g, "\\'");

// [screen name, clicks in order]
const SCREENS = [
  ['character-library', ['Characters']],
  ['account', ['Account']],
  ['compare', ['Gallery', 'Compare']],
  ['admin-hardware', ['Switch to Management', 'Hardware & Setup']],
  ['admin-ops', ['RunPod & Ops']],
  ['admin-sources', ['Model Sources']],
  ['admin-models', ['Model Library']],
  ['admin-registry', ['Registry & Providers']],
  ['admin-comfy', ['ComfyUI Import']],
  ['admin-policy', ['Content Policy']],
];

const s = await openSession(BOOT.url, { bundleId: BOOT.bundle_id, appPath: BOOT.app_path, extra: { 'appium:noReset': true, 'appium:skipAppKill': true } });
const pid = appPid(BOOT.app_path);
async function click(label) {
  for (const v of [`elementType IN {9, 42, 75} AND label ==[c] '${q(label)}'`, `elementType IN {9, 42, 75} AND label CONTAINS[c] '${q(label)}'`]) {
    const el = await s.find('-ios predicate string', v).catch(() => null);
    if (el) { await s.click(el); await sleep(1500); return true; }
  }
  return false;
}
try {
  for (const [name, path] of SCREENS) {
    resizeWindow(pid, 1280, 800); await sleep(800);
    let ok = true;
    for (const label of path) {
      // after a phone-width look the menu may be folded behind the top bar's toggle
      if (!(await click(label)) && !((await click('Toggle Sidebar')) && (await click(label)))) {
        ok = false; console.log(`SCREEN ${name} FAIL no control "${label}" on view`); break;
      }
    }
    if (!ok) continue;
    for (const [w, h] of [[1280, 800], [390, 844]]) {
      resizeWindow(pid, w, h); await sleep(1500);
      const n = parseSource(await s.source());
      const err = n.find((x) => /unhandled error|exception|Reload/i.test(`${x.label || ''} ${x.value || ''}`));
      windowShot(pid, `${OUT}/${name}-${w}.png`);
      console.log(`SCREEN ${name} @${w} ${err ? `ERROR "${(err.label || err.value || '').slice(0, 80)}"` : 'drawn'} ${OUT}/${name}-${w}.png`);
    }
  }
} finally {
  resizeWindow(pid, 1280, 800);
  await s.close();
}
