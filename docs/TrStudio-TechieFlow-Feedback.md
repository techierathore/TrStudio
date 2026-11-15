# TechieFlow feedback — found while building TrStudio

| | |
|---|---|
| App | TrStudio |
| Upstream | TechieFlow |
| Updated | 2026-11-15 |

## Summary

2 entries: 0 blocking now, 2 filed and not blocking, 0 fixed upstream.

Nothing is blocked by the framework itself: the Mac app could not be driven tonight because macOS asks for an administrator password before Automation Mode comes on, which only the owner can give, and TF-001 is why the boot script did not say so.

## Entries

### TF-001 — The Mac boot script misses the Automation Mode password check and names the wrong fix

- **Severity:** minor
- **Blocks:** no — the cause was found by running the mac2 helper build by hand, and the run went on to the next stage
- **Repro:** on macOS 27.2 with Xcode 27.2, `automationmodetool` prints `This device requires user authentication to enable Automation Mode.`; run `bash .tfcore/utils/tf-verify-boot.sh start --head maccatalyst`
- **Expected:** the script stops before the build with "macOS asks for a password before Automation Mode comes on … sudo automationmodetool enable-automationmode-without-authentication"
- **Actual:** line 528 greps `'^This device REQUIRES user authentication'`, which is case-sensitive, so the check never matches; the script builds, the mac2 helper exits with code 65, and the reason printed points at Accessibility permissions. The helper's own log says `Timed out while enabling automation mode.`
- **Encountered in:** stage 2 of the 15 Nov 2026 night run (real-app checks for phase 3)
- **Workaround:** none in TrStudio; the one-time `sudo` command is listed for the owner
- **Suggested fix:** `grep -qi 'requires user authentication'`, and map xcodebuild code 65 with "automation mode" in the helper output to the same fix line

### TF-002 — The mockup comparison reads a gradient button as "neutral" and a solid one as "accent"

- **Severity:** minor
- **Blocks:** no — the control name was put on a box around the button, as Login already does, and the work carried on
- **Repro:** a mockup button `class="btn brand"` whose background is a `linear-gradient`, and the app's TrBlazeUI `Button` with the same gradient over `bg-primary`; run `tf-mockup-parity.sh --screen forgot-password=/forgot-password`
- **Expected:** both read as the brand colour, no finding
- **Actual:** `semantic colour differs — mockup neutral, app accent` and `mockup renders this as a plain element, app renders it as a badge/pill` on the button (`btn-send-reset-link`, 15 Nov 2026)
- **Encountered in:** decision 11 naming run, Forgot password (REQ-UI-003)
- **Workaround:** the name sits on a `span` around the button, so the button itself is not compared
- **Suggested fix:** take the first colour stop of a `background-image` gradient as the background colour before classifying, on both sides

