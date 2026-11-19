# TrStudio — Misses

| | |
|---|---|
| App | TrStudio |
| Count | 135 logged: 23 open, 112 fixed, 0 will not fix |
| Source | `docs/metrics/misses.jsonl`, one row per miss record. Rewritten by `tf-misses-md.sh` on every new record. Never edit it: a wrong row is corrected by a new record. |
| Updated | 2026-11-18 |

**Whose gap** answers the four questions of the miss protocol: **the app's spec** did not say it, so the checklist line is fixed; **the framework never said it**, so one requirement line and a check are added; **the check was too weak** (a review, or a script that did not fire), so the check is fixed; **said and ignored**, so the rule becomes a hook or is deleted. **not sorted** means the record predates the sort or nobody has answered yet; `bash .tfcore/utils/tf-emit.sh --amend <miss> sort <spec|unsaid|weak-check|ignored>` completes it.

## Open (23)

| Miss | Found | Whose gap | What went wrong |
|---|---|---|---|
| MISS-TrStudio-20261117-77 (REQ-FN-065) | 2026-11-17 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-76 (REQ-FN-054) | 2026-11-17 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-57 (REQ-UI-052) | 2026-11-17 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-49 (REQ-FN-051) | 2026-11-17 by owner | the check was too weak | Video studio's image-to-video sent only the start frame's file name, never the picture (same cause as REQ-FN-052); fixed. Live Venice clip 18 Nov: asked 2 s 9:16, got 480x848 2.04 s (/bin/zsh.05) |
| MISS-TrStudio-20261117-48 (REQ-FN-052) | 2026-11-17 by owner | the check was too weak | Video studio sent only the pinned frames' file names, never the pictures, so the model never saw them (also the start frame of every image-to-video job). Fixed: each frame is uploaded as the creator's own picture and sent as sourceAssetId / endImageAssetId; check p2-venice.spec.ts REQ-FN-052 |
| MISS-TrStudio-20261117-47 (REQ-FN-010) | 2026-11-17 by owner | the check was too weak | Real Mac app: Provision isolated Python refused the host as 'Mac Catalyst (Arm64)' (same Catalyst-is-not-macOS cause), and the row stayed at 0 % with the failure seen only after Logs (the page never followed the step). Both fixed (HostPlatform.IsMac; the page follows a running step). Then macOS refu |
| MISS-TrStudio-20261117-43 (REQ-UI-040) | 2026-11-17 by owner | the check was too weak | Model box cut a long seeded model name at 1280 px (owner screen check 17 Nov); fixed with a wrapping label, check video-model-fit.spec.ts |
| MISS-TrStudio-20261117-40 (REQ-FN-065) | 2026-11-17 by gate | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-39 (REQ-FN-054) | 2026-11-17 by gate | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-38 (REQ-FN-053) | 2026-11-17 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-37 (REQ-FN-052) | 2026-11-17 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-36 (REQ-FN-051) | 2026-11-17 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-33 (REQ-FN-038) | 2026-11-17 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-29 (REQ-FN-031) | 2026-11-17 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-27 (REQ-FN-028) | 2026-11-17 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-22 (REQ-FN-021) | 2026-11-17 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-21 (REQ-FN-020) | 2026-11-17 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-20 (REQ-FN-019) | 2026-11-17 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-11 (REQ-UI-052) | 2026-11-17 by gate | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-08 (REQ-UI-040) | 2026-11-17 by gate | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-01 (REQ-UI-040) | 2026-11-17 by owner | the check was too weak | Video studio's Model box cuts a long seeded model name at 1280 px. Root cause: the box is a one-line native select in a 320 px column (240 px for text); names of 264-350 px were cut, and the sweep only checked that the screen rendered, never that the chosen text fit. Fixed: the chosen name is drawn |
| MISS-TrStudio-20261115-09 (REQ-FN-047) | 2026-11-15 by owner | the check was too weak | ComfyUI import: every model file a graph names is reported missing, because the dependency scan never asks ComfyUI what it holds; so a freshly imported real graph (one naming a checkpoint) never reads known-good and Save as model stays disabled. Needs a real ComfyUI to fix and prove. |
| MISS-TrStudio-20261115-08 (REQ-FN-046) | 2026-11-15 by owner | the check was too weak | ComfyUI import: Run drew a made-up per-node progression ('Completed 5 of 5 nodes · live WebSocket preview') when the debug service could not be reached; it now says the service was not reached and shows no results |

## Fixed (112)

| Miss | Found | Closed | Whose gap | What went wrong |
|---|---|---|---|---|
| MISS-TrStudio-20261118-04 (REQ-UI-070) | 2026-11-18 by owner | 2026-11-18 by log-miss | the app's spec | Hardware's mockup still drew the old Python and ComfyUI download steps after decision 17B, so the render check failed; and on open the address field showed the default, not the kept address, when the kept one did not answer. Mockup and page fixed |
| MISS-TrStudio-20261118-03 (REQ-NFR-010) | 2026-11-18 by owner | 2026-11-18 by log-miss | the check was too weak | REQ-NFR-010's browser test inserted fixed row ids, so a second run on the same host hit the unique key on the kept fresh row; ids now unique per run |
| MISS-TrStudio-20261118-02 (REQ-FN-010) | 2026-11-18 by owner | 2026-11-18 by log-miss | the framework never said it | REQ-FN-010's browser test still checked the replaced acceptance (provision Python on Hardware) after decision 17B changed it; rewritten to the Mac acceptance |
| MISS-TrStudio-20261118-01 (REQ-FN-009) | 2026-11-18 by owner | 2026-11-18 by log-miss | the framework never said it | Hardware's ComfyUI badge stayed orange after a successful connect: the TrBlazeUI Badge kept its first Class (TR-016); fixed with @key; about 5 minutes to find (builder's smoke) |
| MISS-TrStudio-20261117-79 (REQ-FN-025) | 2026-11-17 by owner | 2026-11-17 by log-miss | the app's spec | Gallery mockup gave each tile's picture art a fixed colour, but the art stands for the user's own picture, so its colour is data; the mockup now marks the tile art as sample data |
| MISS-TrStudio-20261117-78 (REQ-FN-067) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-75 (REQ-FN-040) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-74 (REQ-FN-039) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-73 (REQ-FN-036) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-72 (REQ-FN-034) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-71 (REQ-FN-033) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-70 (REQ-FN-029) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-69 (REQ-FN-025) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-68 (REQ-FN-024) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-67 (REQ-FN-023) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-66 (REQ-FN-022) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-65 (REQ-FN-014) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-64 (REQ-UI-082) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-63 (REQ-UI-081) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-62 (REQ-UI-080) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-61 (REQ-UI-076) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-60 (REQ-UI-073) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | the check was too weak | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-59 (REQ-UI-070) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-58 (REQ-UI-060) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-56 (REQ-UI-051) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-55 (REQ-UI-050) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | the check was too weak | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-54 (REQ-UI-030) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-53 (REQ-UI-020) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | the check was too weak | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-52 (REQ-UI-011) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-51 (REQ-UI-010) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | the check was too weak | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-50 (REQ-UI-005) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-46 (REQ-FN-008) | 2026-11-17 by owner | 2026-11-17 by triage-and-fix | the check was too weak | Real Mac app: Hardware & Setup said MPS + CoreML Unavailable, CPU only, and no GPU cores, on an Apple Silicon Mac. Root cause: inside the Mac app the engine is a Mac Catalyst process and IsOSPlatform(OSX) is false there, so the Apple probe never ran; the check ran only as plain macOS. Fixed with Hos |
| MISS-TrStudio-20261117-45 (REQ-FN-016) | 2026-11-17 by owner | 2026-11-17 by triage-and-fix | said and ignored | Login's Sign in measured as neutral against the mockup's accent: the mockup's sign-in id sat on a plain wrapper, not on the button; moved onto the button (parity now PASS at 1280/390, light and dark) |
| MISS-TrStudio-20261117-44 (REQ-UI-001) | 2026-11-17 by owner | 2026-11-17 by triage-and-fix | said and ignored | Login's Sign in measured as neutral against the mockup's accent: the mockup's sign-in id sat on a plain wrapper, not on the button; moved onto the button (parity now PASS at 1280/390, light and dark) |
| MISS-TrStudio-20261117-42 (REQ-RAG-005) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261117-41 (REQ-FN-067) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-35 (REQ-FN-040) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-34 (REQ-FN-039) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-32 (REQ-FN-036) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-31 (REQ-FN-034) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-30 (REQ-FN-033) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-28 (REQ-FN-029) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-26 (REQ-FN-025) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-25 (REQ-FN-024) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-24 (REQ-FN-023) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-23 (REQ-FN-022) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-19 (REQ-FN-014) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-18 (REQ-UI-082) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-17 (REQ-UI-081) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-16 (REQ-UI-080) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-15 (REQ-UI-076) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-14 (REQ-UI-073) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-13 (REQ-UI-070) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-12 (REQ-UI-060) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-10 (REQ-UI-051) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-09 (REQ-UI-050) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-07 (REQ-UI-030) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-06 (REQ-UI-020) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-05 (REQ-UI-011) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-04 (REQ-UI-010) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-03 (REQ-UI-005) | 2026-11-17 by gate | 2026-11-18 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261117-02 (REQ-FN-116) | 2026-11-17 by owner | 2026-11-17 by triage-and-fix | the app's spec | Owner asked to remove the placeholder HeyGen key through the app's own screen, but Registry & providers has no way to remove a key: the engine's delete route was never given a button, and no requirement asked for one. |
| MISS-TrStudio-20261116-09 (REQ-FN-097) | 2026-11-16 by owner | 2026-11-17 by triage-and-fix | the app's spec | A Mac Catalyst app can only use the data protection keychain, whose access group must be authorised by a Mac provisioning profile; the design never said so, so the unsigned app cannot keep keys or a kept sign-in |
| MISS-TrStudio-20261116-08 (REQ-FN-016) | 2026-11-16 by owner | 2026-11-16 by amend-docs | the app's spec | AppManager sends its own role names (user 1 Admin, user 2 Manager); TrStudio read Admin as Operator and never knew Admin=Owner, Manager=Operator, User=Creator, so both test users landed wrong |
| MISS-TrStudio-20261116-07 (REQ-UI-005) | 2026-11-16 by owner | 2026-11-16 by triage-and-fix | the check was too weak | Real Mac app at 390 wide: choosing a screen from the folded menu left the menu sheet open over that screen (and over the page after widening back). Fixed: the sidebar closes the phone menu on every route change |
| MISS-TrStudio-20261116-06 (REQ-UI-010) | 2026-11-16 by owner | 2026-11-16 by triage-and-fix | the check was too weak | Real Mac app @1280: the Render tier column covered the Script box; under the app's CSS zoom (about 1.27) neither the 1100px media query nor a container query folds the 300 + 1fr + 300 studio grid. Fixed: the studio row wraps by itself (flex-wrap), same wide layout; zoom tests at 1.25 and 1.6 in Chro |
| MISS-TrStudio-20261116-05 (REQ-FN-094) | 2026-11-16 by owner | 2026-11-16 by triage-and-fix | the check was too weak | Real Mac app: a job closed mid-run (stub, 10%) was marked Failed with 'TrStudio was closed while this job was running' on the next open, but the reason showed nowhere: Account's Recent jobs reads the generation log, which the start-up marking never wrote. Fixed: the marking writes the failed log row |
| MISS-TrStudio-20261116-04 (REQ-FN-104) | 2026-11-16 by owner | 2026-11-16 by triage-and-fix | the check was too weak | Real Mac app: at the default 1.6 zoom the sidebar of every signed-in screen drew 1.6 windows tall; its footer (Sign out, surface switch) sat at y=2377 in a window ending at 1651, out of reach. Fixed: full-height utilities divide by --tr-zoom; the sidebar scrolls and pins its footer |
| MISS-TrStudio-20261116-03 (REQ-FN-099) | 2026-11-16 by owner | 2026-11-16 by triage-and-fix | the check was too weak | Real Mac app: with Remember me ticked, sign-in as test user 1 failed outright ('Sign-in failed'): keeping the sign-in uses the Keychain, which refuses this unsigned build (MissingEntitlement). Fixed: the person is signed in for the session and told the sign-in could not be kept |
| MISS-TrStudio-20261116-02 (REQ-FN-096) | 2026-11-16 by owner | 2026-11-16 by triage-and-fix | the check was too weak | Real Mac app: saving a key on Registry & providers failed with 'Couldn't store provider keys — check your admin session'; the real cause is the macOS Keychain refusing this unsigned build (MissingEntitlement), so no Keychain item is made and no key is kept. Fixed: the page now says the Keychain refu |
| MISS-TrStudio-20261116-01 (REQ-FN-086) | 2026-11-16 by owner | 2026-11-16 by triage-and-fix | the check was too weak | Real Mac app: sign-in as test user 1 refused with 'A public API key call must name its device in the X-Device-Id header (1 to 200 characters)'; the app sent no device id. Fixed: a per-install id kept in the data folder is sent as X-Device-Id |
| MISS-TrStudio-20261115-07 (REQ-UI-004) | 2026-11-15 by owner | 2026-11-17 by triage-and-fix | the check was too weak | Setup required: the Waiting for admin setup badge used a blended amber, not the design's --warn family (app side changed); the mockup's Local library ready badge had no .badge.ok style (mockup side changed) |
| MISS-TrStudio-20261115-06 (REQ-FN-015) | 2026-11-15 by owner | 2026-11-15 by triage-and-fix | the check was too weak | Register: the chosen account type colour differs from the mockup (same defect as REQ-UI-002, app side changed) |
| MISS-TrStudio-20261115-05 (REQ-UI-002) | 2026-11-15 by owner | 2026-11-15 by triage-and-fix | the check was too weak | Register: the chosen account type is drawn as a solid primary button; the design's Selected/active rule and the mockup draw it as a brand-soft segment with brand-ink text (app side changed) |
| MISS-TrStudio-20261115-04 (REQ-FN-016) | 2026-11-15 by gate | 2026-11-17 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261115-03 (REQ-FN-015) | 2026-11-15 by gate | 2026-11-15 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261115-02 (REQ-UI-002) | 2026-11-15 by gate | 2026-11-15 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261115-01 (REQ-UI-001) | 2026-11-15 by gate | 2026-11-17 by triage-and-fix | not sorted | no sentence recorded (regression, src) |
| MISS-TrStudio-20261114-24 (REQ-UI-095) | 2026-11-14 by owner | 2026-11-15 by triage-and-fix | the check was too weak | A refused Venice video call whose answer gives its reset time in milliseconds crashed the error reader (ArgumentOutOfRangeException), and a 400 dropped the fields Venice named, so Venice's own reason never reached the screen |
| MISS-TrStudio-20261114-23 (REQ-FN-104) | 2026-11-14 by owner | 2026-11-15 by triage-and-fix | the app's spec | Zoom keeps the Mac app's screens laid out to the window |
| MISS-TrStudio-20261114-22 (REQ-FN-077) | 2026-11-14 by owner | 2026-11-15 by triage-and-fix | the check was too weak | The fixed-character check uses a stand-in model id without 'reference-to-video', so the tick it expects after reload was never set |
| MISS-TrStudio-20261114-21 (REQ-UI-097) | 2026-11-14 by owner | 2026-11-15 by triage-and-fix | the check was too weak | The Fill from Venice check uses a stand-in model id without 'reference-to-video', so 'Keeps a fixed character' is rightly left unticked and the check fails |
| MISS-TrStudio-20261114-20 (REQ-UI-091) | 2026-11-14 by owner | 2026-11-15 by triage-and-fix | the check was too weak | The Image to video check clicks the words 'Image to video', but the tab reads 'Image → Video' and carries no test name |
| MISS-TrStudio-20261114-19 (REQ-UI-089) | 2026-11-14 by owner | 2026-11-15 by triage-and-fix | the check was too weak | The Text to video check never switches Video studio to Text to video, so the text-to-video rows are never listed |
| MISS-TrStudio-20261114-18 (REQ-UI-086) | 2026-11-14 by owner | 2026-11-15 by triage-and-fix | the check was too weak | Generating with a Venice model on Image studio shows a coloured stand-in tile with no Venice badge: the job-result call never says which provider made the picture |
| MISS-TrStudio-20261114-17 (REQ-UI-097) | 2026-11-14 by gate | 2026-11-15 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-16 (REQ-UI-096) | 2026-11-14 by gate | 2026-11-15 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-15 (REQ-UI-091) | 2026-11-14 by gate | 2026-11-15 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-14 (REQ-UI-089) | 2026-11-14 by gate | 2026-11-15 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-13 (REQ-UI-086) | 2026-11-14 by gate | 2026-11-15 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-12 (REQ-UI-085) | 2026-11-14 by gate | 2026-11-15 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-11 (REQ-FN-077) | 2026-11-14 by gate | 2026-11-15 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-10 (REQ-UI-084) | 2026-11-14 by gate | 2026-11-15 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-09 (REQ-UI-084) | 2026-11-14 by agent-review | 2026-11-15 by triage-and-fix | the check was too weak | On Registry and providers, opening Add or Edit model ended the page (a list carried a control name the library list cannot take), and a newly added model row was saved switched off so no studio listed it; both fixed. |
| MISS-TrStudio-20261114-08 (REQ-UI-075) | 2026-11-14 by gate | 2026-11-15 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-07 (REQ-UI-004) | 2026-11-14 by gate | 2026-11-17 by triage-and-fix | not sorted | no sentence recorded (partial-implementation, src) |
| MISS-TrStudio-20261114-06 (REQ-FN-014) | 2026-11-14 by agent-review | 2026-11-14 by triage-and-fix | the check was too weak | With personal mode off, Premium on Avatar studio was queued without checking the licence; the tier check was registered but never called, and the route now refuses with the upgrade message. |
| MISS-TrStudio-20261114-05 (REQ-FN-026) | 2026-11-14 by agent-review | 2026-11-14 by triage-and-fix | the check was too weak | Reproduce on Projects answered not found (the screen sent a job id where a log id was expected) and a reproduced run lost its seed and project; it now takes either id and keeps both. |
| MISS-TrStudio-20261114-04 (REQ-UI-005) | 2026-11-14 by agent-review | 2026-11-14 by triage-and-fix | the check was too weak | In light mode the green and amber status badges on eight screens used their tint colour for text, below 3:1 contrast; they now use darker text colours. |
| MISS-TrStudio-20261114-03 (REQ-UI-005) | 2026-11-14 by agent-review | 2026-11-15 by triage-and-fix | the check was too weak | The app frame and 18 phase-1 screens never carried their mockups' control names, so the screen checker graded every signed-in screen empty; the frame's names are added, the screens' own names wait for decision 11. |
| MISS-TrStudio-20261114-02 (REQ-UI-098) | 2026-11-14 by agent-review | 2026-11-14 by build-phase | the check was too weak | Alerts on Login, Register, Forgot password and four admin screens showed an empty gap instead of their icon: three icon names (alert-triangle, alert-circle, check-circle) are not in the icon package; the screens now use its names. |
| MISS-TrStudio-20261114-01 (REQ-FN-101) | 2026-11-14 by agent-review | 2026-11-14 by build-phase | the check was too weak | On the test host the sign-in-again notice flashed and vanished: Login's start-up ran twice (pre-render, then live), and the first run used up the kept sign-in; it now runs once after the first live render. |
| MISS-TrStudio-20261113-09 (REQ-UI-004) | 2026-11-13 by agent-review | 2026-11-14 by triage-and-fix | the check was too weak | On Setup required the theme toggle overlapped the card at phone width; auth screens now start below the toggle (pre-existing since phase 1). |
| MISS-TrStudio-20261113-08 (REQ-UI-004) | 2026-11-13 by agent-review | 2026-11-14 by triage-and-fix | the check was too weak | Login and Setup required never carried their mockups' control names, so the framework's screen check could not map them; the names were added (pre-existing since phase 1). |
| MISS-TrStudio-20261113-07 (REQ-UI-098) | 2026-11-13 by agent-review | 2026-11-13 by build-phase | the check was too weak | The headless checks typed before the screen was interactive, so the typing was lost and the click waited on a disabled button. |
| MISS-TrStudio-20261113-06 (REQ-NFR-020) | 2026-11-13 by agent-review | 2026-11-13 by build-phase | the framework never said it | The test host was marked not publishable, which broke the framework's boot step that publishes it before running. |
| MISS-TrStudio-20261113-05 (REQ-UI-099) | 2026-11-13 by agent-review | 2026-11-13 by build-phase | the app's spec | An unreachable sign-in service showed a technical error with its address instead of the plain message. |
| MISS-TrStudio-20261113-04 (REQ-FN-097) | 2026-11-13 by agent-review | 2026-11-13 by build-phase | the app's spec | A key saved under a different unlock key failed with a cryptic decryption error; it now says to enter the key again on Registry and providers. |
| MISS-TrStudio-20261113-03 (REQ-FN-091) | 2026-11-13 by agent-review | 2026-11-13 by build-phase | the app's spec | The job runner was shut down twice by the service container and threw on the second time; shutting down is now done once. |
| MISS-TrStudio-20261113-02 (REQ-FN-093) | 2026-11-13 by agent-review | 2026-11-13 by build-phase | the app's spec | Start-up recovery could queue a job a second time while it was already waiting, so it ran twice; the runner now holds each job once. |
| MISS-TrStudio-20261113-01 (REQ-NFR-016) | 2026-11-13 by agent-review | 2026-11-14 by amend-docs | the app's spec | The Mac app would not link: ONNX Runtime ships no Mac Catalyst library, so local ONNX picture generation cannot run inside the Mac app until a Mac build of it is shipped. |
| MISS-TrStudio-20261112-01 (REQ-UI-086) | 2026-11-12 by agent-review | 2026-11-15 by triage-and-fix | not sorted | No route returned a generated picture's file, so no studio could show the picture a provider returned; an owner-only asset content route was added. |
