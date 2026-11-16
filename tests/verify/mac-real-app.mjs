// Real Mac app checks (owner run of 15 Nov 2026, part B), driven through the Appium endpoint that
// tf-verify-boot.sh --head maccatalyst started. Test users come from tests/verify/testsettings*.json;
// no email, password or key is ever printed. Each step prints one line: STEP <name> PASS|FAIL <why>.
//
//   node tests/verify/mac-real-app.mjs <step> [user]
//   steps: signin <n> [remember]   sign in as test user n (ticking "Remember me" when asked)
//          signout                 click Sign out in the sidebar
//          state                   print whether the app shows the sign-in form or a signed-in shell
//          quit                    quit the app;   open   start it again (new session) and print state
//          shot <name>             window screenshot into tests/.artifacts/verify/mac/<name>.png
//          click <label>           click the control labelled so (exact, else contains)
//          type <label> <text>     type into the text field whose label or placeholder holds <label>
import { mkdirSync, readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { openSession, parseSource, appPid, windowShot } from '../../.tfcore/utils/tf-appium.mjs';
import { testSettings } from '../../.tfcore/utils/tf-testsettings.mjs';

const BOOT = JSON.parse(readFileSync('tests/.artifacts/verify/boot-4723.json', 'utf8'));
const URL = BOOT.url || 'http://localhost:4723';
const BUNDLE = BOOT.bundle_id;
const APP = BOOT.app_path;
const OUT = 'tests/.artifacts/verify/mac';
mkdirSync(OUT, { recursive: true });
const sleep = (ms) => new Promise((ok) => setTimeout(ok, ms));
const q = (t) => t.replace(/\\/g, '\\\\').replace(/'/g, "\\'");
const [step, a1, a2, ...rest] = process.argv.slice(2);

function running() {
  try { execFileSync('pgrep', ['-f', `${APP}/Contents/MacOS/`]); return true; } catch { return false; }
}

async function session() {
  return openSession(URL, { bundleId: BUNDLE, appPath: APP, extra: { 'appium:noReset': true, 'appium:skipAppKill': true } });
}

async function nodes(s) { return parseSource(await s.source()); }

// Login draws "Welcome back to TrStudio" and a Password field; the signed-in shell has the sidebar toggle.
// (A key field on Registry is a secure field too, so a secure field alone is not the sign-in form.)
const onLogin = (n) => n.some((x) => /Welcome back to TrStudio/i.test(`${x.label || ''} ${x.value || ''}`))
  && n.some((x) => x.type === 'SecureTextField' && /Password/i.test(x.label || ''));
const inShell = (n) => n.some((x) => x.type === 'Button' && /Toggle Sidebar/i.test(x.label || ''));

async function stateOf(s) {
  const t0 = Date.now();
  let n = await nodes(s);
  while (Date.now() - t0 < 20000 && !onLogin(n) && !inShell(n)) { await sleep(500); n = await nodes(s); }
  if (onLogin(n)) return 'sign-in form';
  if (inShell(n)) return 'signed in';
  return 'unknown';
}

async function control(s, text, types = '9, 10, 12, 42, 75') {
  const win = await s.find('-ios predicate string', 'elementType == 4').catch(() => null);
  for (const v of [`elementType IN {${types}} AND (label ==[c] '${q(text)}' OR title ==[c] '${q(text)}')`,
    `elementType IN {${types}} AND (label CONTAINS[c] '${q(text)}' OR title CONTAINS[c] '${q(text)}')`]) {
    const el = await s.find('-ios predicate string', v, win).catch(() => null);
    if (el) return el;
  }
  return null;
}

function shot(name) {
  const pid = appPid(APP);
  const file = `${OUT}/${name}.png`;
  if (pid) windowShot(pid, file);
  return file;
}

async function signIn(s, n, remember) {
  const users = testSettings().TestUsers || {};
  const u = users[n] || users[String(n)];
  if (!u || !u.Email || !u.Password) return `FAIL test user ${n} has no email and password in the settings files`;
  if ((await stateOf(s)) !== 'sign-in form') return 'FAIL the sign-in form is not on view';
  const win = await s.find('-ios predicate string', 'elementType == 4');
  const email = await s.find('-ios predicate string', 'elementType == 49', win);
  const pwd = await s.find('-ios predicate string', "elementType == 50 AND label CONTAINS[c] 'Password'", win);
  const clear = async (el) => {
    await s.click(el);
    await s.call('POST', '/actions', { actions: [{ type: 'key', id: 'keyboard', actions: [
      { type: 'keyDown', value: '\uE03D' }, { type: 'keyDown', value: 'a' }, { type: 'keyUp', value: 'a' }, { type: 'keyUp', value: '\uE03D' },
      { type: 'keyDown', value: '\uE003' }, { type: 'keyUp', value: '\uE003' }] }] });
    await s.call('DELETE', '/actions').catch(() => {});
  };
  let ok = false;
  for (let i = 0; i < 3 && !ok; i++) {
    await clear(email); await s.type(email, u.Email); await sleep(500);
    ok = (await nodes(s)).some((x) => x.type === 'TextField' && x.value === u.Email);
  }
  if (!ok) return 'FAIL the email did not stay in the Email field';
  await clear(pwd); await s.type(pwd, u.Password); await sleep(500);
  if (!(await nodes(s)).some((x) => x.type === 'TextField' && x.value === u.Email)) return 'FAIL the email was lost after typing the password';
  if (remember) {
    const box = await control(s, 'Remember me', '40, 12, 9');
    if (!box) return 'FAIL no "Remember me" box found';
    await s.click(box); await sleep(600);
    const ticked = (await nodes(s)).some((x) => x.type === 'Switch' && /Remember me/i.test(x.label || '') && String(x.value) === '1');
    if (!ticked) return 'FAIL "Remember me" did not tick';
  }
  await sleep(600);
  const btn = await control(s, 'Sign in', '9');
  if (!btn) return 'FAIL no Sign in button';
  await s.click(btn);
  const t0 = Date.now();
  while (Date.now() - t0 < 30000) {
    await sleep(1000);
    const n2 = await nodes(s);
    if (inShell(n2)) return `PASS signed in as test user ${n}${remember ? ' with Remember me ticked' : ''}`;
    const err = n2.find((x) => /failed|unreachable|not accepted|invalid|incorrect/i.test(`${x.label || ''} ${x.value || ''}`));
    if (err) return `FAIL still on Login: "${(err.label || err.value || '').slice(0, 120)}"`;
  }
  return 'FAIL still on Login 30 s after Sign in';
}

let s = null;
try {
  if (step === 'quit') {
    execFileSync('osascript', ['-e', `tell application id "${BUNDLE}" to quit`]);
    for (let i = 0; i < 20 && running(); i++) await sleep(500);
    console.log(`STEP quit ${running() ? 'FAIL the app is still running' : 'PASS the app quit'}`);
    process.exit(0);
  }
  if (step === 'open') {
    execFileSync('open', ['-a', APP]);
    await sleep(8000);
  }
  s = await session();
  switch (step) {
    case 'signin': console.log(`STEP signin ${await signIn(s, a1, a2 === 'remember')}`); break;
    case 'signout': {
      // the identity chip in the top bar (its title is "Sign out"; the sidebar footer button can sit off view)
      const el = (await control(s, 'T', '9')) || (await control(s, 'Sign out'));
      if (!el) { console.log('STEP signout FAIL no Sign out control'); break; }
      await s.click(el); await sleep(2000);
      console.log(`STEP signout ${(await stateOf(s)) === 'sign-in form' ? 'PASS back on the sign-in form' : 'FAIL not back on Login'}`);
      break;
    }
    case 'open':
    case 'state': console.log(`STEP ${step} PASS the app shows: ${await stateOf(s)}`); break;
    case 'shot': console.log(`STEP shot PASS ${shot(a1 || 'window')}`); break;
    case 'click': {
      const el = await control(s, [a1, a2, ...rest].filter(Boolean).join(' '));
      if (!el) { console.log(`STEP click FAIL no control "${a1}"`); break; }
      await s.click(el); await sleep(1500);
      console.log(`STEP click PASS clicked "${[a1, a2, ...rest].filter(Boolean).join(' ')}"`);
      break;
    }
    case 'type': {
      const win = await s.find('-ios predicate string', 'elementType == 4');
      const el = await s.find('-ios predicate string', `elementType IN {49, 50, 52} AND (label CONTAINS[c] '${q(a1)}' OR placeholderValue CONTAINS[c] '${q(a1)}' OR title CONTAINS[c] '${q(a1)}')`, win).catch(() => null);
      if (!el) { console.log(`STEP type FAIL no field "${a1}"`); break; }
      await s.click(el); await s.type(el, [a2, ...rest].join(' '));
      console.log(`STEP type PASS typed into "${a1}"`);
      break;
    }
    case 'dump': {
      const n = await nodes(s);
      for (const x of n) if (x.label || x.value) console.log(`${x.type}\t${(x.label || '').slice(0, 70)}\t${(x.value || '').slice(0, 40)}\t@${x.x},${x.y} ${x.w}x${x.h}`);
      break;
    }
    default: console.log(`unknown step ${step}`);
  }
} catch (e) {
  console.log(`STEP ${step} FAIL Appium: ${String(e.message).slice(0, 200)}`);
} finally {
  if (s) await s.close();
}
