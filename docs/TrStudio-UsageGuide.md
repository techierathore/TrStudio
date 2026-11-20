# TrStudio — Usage Guide

| | |
|---|---|
| App | TrStudio |
| Kind | app |
| Size | Large |
| Date | 2026-11-20 |

How to run and test TrStudio, one desktop app with the engine inside. The first version is for Mac; Windows comes in a later version. The smoke and verify agents and the owner's acceptance testing use the same test users and the same walkthrough below. The end-user manual is the Product Guide (`*productguide TrStudio`); this is the internal run and test reference.

## Test users

Identity comes from AppManager; TrStudio stores only the AppManager `userId`, never a password. Users 1 and 2 were registered by the owner; users 3 and 4 were registered on 18 Nov 2026 through AppManager's public register call (`POST /AuthSvc/register`, application 5) by `node .tfcore/utils/tf-register-test-users.mjs`, which made their passwords. Each password lives in the git-ignored `tests/verify/testsettings.Local.json`, laid over the committed `tests/verify/testsettings.json`; it is never written here. For switching roles in smoke runs, a Debug build of the app opens signed in as `Desktop:DevRole` from the git-ignored `src/TrStudio/appsettings.json`, and the test host serves the developer sign-in page `/dev/session`.

| # | User | Password source | AppManager role → TrStudio role | Exists |
|---|---|---|---|---|
| 1 | techierathore@gmail.com | `TestUsers:1:Password` in the git-ignored `tests/verify/testsettings.Local.json` | Admin → **Owner** (creator and management screens, all tiers); the owner walkthrough. Never create or overwrite it | yes |
| 2 | tflensdemo@techierathore.com | `TestUsers:2:Password` in the git-ignored `tests/verify/testsettings.Local.json` | Manager → **Operator** (management screens only); the second user, and the unattended-run user for the management screens | yes |
| 3 | creator@trstudio.test | `TestUsers:3:Password` in the git-ignored `tests/verify/testsettings.Local.json` | User → **Creator**; the creator for the isolation checks | yes (AppManager user 19, role User, 18 Nov 2026) |
| 4 | premium@trstudio.test | `TestUsers:4:Password` in the git-ignored `tests/verify/testsettings.Local.json` | User → **Creator** with a Premium licence (exercises REQ-FN-014) | yes (AppManager user 20, role User, 18 Nov 2026); no Premium licence yet — the owner assigns it in AppManager |

AppManager has three application roles and TrStudio maps them to its own (owner, 17 Nov 2026; REQ-FN-115): Admin is Owner, Manager is Operator, User is Creator. AppManager sends the role as `applicationRole` when a user signs in.

A verify test never creates, deletes or changes an account on a shared AppManager; the register-creates-an-account path is verified by hand once per release and recorded in the checklist with the date, and the automated row checks only the form's validation and the email-already-in-use path using test user 1. The Register test signs in as test user 1 before it submits anything; the test users are registered only by the owner, with `node .tfcore/utils/tf-register-test-users.mjs`.

**Who lands where** (BRD-84 role landing, BRD-85 first-run gate: setup is complete when at least one model source is enabled and at least one registry model is ready):

| Role | Lands on | While setup is incomplete |
|---|---|---|
| Owner (1) | Avatar studio `/avatar`; a sidebar-footer link switches to the management screens | Guided setup `/admin/setup` |
| Operator (2) | RunPod & ops `/admin/ops` | Hardware & readiness `/admin/setup/hardware`, then guided setup |
| Creators (3, 4) | Avatar studio `/avatar` | `/setup-required` ("ask your admin") |

Checked on the real Mac app on 18 Nov 2026 with user 3: on this install, which has no enabled model source, user 3 lands on Setup required and user 1 on Hardware & Setup; with a model source turned on for the check, user 3 lands on Avatar studio with only the creator screens, and user 1 on Avatar studio with the switch to the management screens. The source used for the check, "Hugging Face (temporary check)", was turned off again (the app has no way to delete a source).

## Execution guide

Prerequisites: a Mac with full Xcode, the .NET 10 SDK and the .NET MAUI workload; two git-ignored files recreated on every machine: `nuget.config` from `nuget.config.template` plus the private feed token (without it: `NU1101: TechieRag`), and `src/TrStudio/appmanager.local.json` from `src/TrStudio/appmanager.template.json` plus the AppManager address, application id and API key (put into the build). No database server and no API process; no setting is read from an environment variable.

```
dotnet restore TrStudio.slnx
dotnet build src/TrStudio/TrStudio.csproj -f net10.0-maccatalyst
open src/TrStudio/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/TrStudio.app
```

On first start the app creates and migrates `TrStudio.db` (plus `TrStudio.vectors.db`) in the user's app data folder; jobs run inside the app. The owner's copy is installed at `/Applications/TrStudio.app` (Debug, signed, 20 Nov 2026). Sign in as user 1. Without the AppManager settings the app still opens and Login says the copy was built without its sign-in settings.

**Phase 2 (Venice):** sign in as user 1 or 3, open Registry & providers, add provider `Venice` with address `https://api.venice.ai/api/v1` and the Venice API key (kept in the local database, protected by the Mac Keychain for that user), press Test connection, then add one registry row per Venice model and task.

**Removing a key:** on Registry & providers, a configured provider shows **Remove key**; press it, then **Remove** (or **Keep**). The key's encrypted row is deleted and the provider shows as not configured at once, with no restart (REQ-FN-116, 18 Nov 2026). The one Keychain item that unlocks the stored keys stays (decision 16).

## How to test, screen by screen

Screens degrade cleanly where a model, key or service is missing; those steps are marked "needs".

### Owner's test, 20 Nov 2026 — 1. Open, sign in, Venice key
- **Sign in as:** user 1, opening Finder › Applications › TrStudio
- **Steps:** 1) double-click TrStudio; type user 1's email and password, leave "Remember me" unticked, click Sign in 2) Registry & Providers › Provider keys › Venice: turn on Enable Venice, address `https://api.venice.ai/api/v1`, paste your Venice key 3) click Test connection, then Save keys
- **Expected:** you land on Hardware & Setup (no model source enabled yet); the ComfyUI card says "Not connected": skip today; Test connection shows a green Connected. Free. All seven parts: about 40 minutes, about 0.15 USD
- **Covers:** REQ-UI-001, REQ-FN-075, REQ-UI-083

### Owner's test, 20 Nov 2026 — 2. Three Venice models
- **Sign in as:** user 1
- **Steps:** 1) + Add model, Provider Venice, Provider model id `venice-sd35`, Fill from Venice, Save 2) the same with `wan-3-0-text-to-video`, then change Display name to `Wan 3.0 text to video`, Save 3) + Add model, Display name `Gemma 4 on Venice`, Task text-chat, Endpoint `https://api.venice.ai/api/v1`, Chat model `google-gemma-4-26b-a4b-it`, API key: the Venice key again, Save
- **Expected:** three new rows with provider Venice (the chat row says Text LLM). Free
- **Covers:** REQ-UI-084, REQ-UI-097, REQ-RAG-006

### Owner's test, 20 Nov 2026 — 3. Prompt studio
- **Sign in as:** user 1
- **Steps:** 1) Switch to Creator studio › Prompt Studio 2) Model Gemma 4 on Venice, click New chat 3) type `Write one short image prompt for a Diwali poster with a rangoli.` and send
- **Expected:** within about 10 seconds a real reply under "GEMMA 4 ON VENICE", with Copy, Save to Library and Send to Image Studio. Under 0.01 USD. The seeded GPT-4o and Llama rows answer "DEV SYNTHETIC": skip them
- **Covers:** REQ-UI-081, REQ-UI-082

### Owner's test, 20 Nov 2026 — 4. Image studio
- **Sign in as:** user 1
- **Steps:** 1) Image Studio, Model "Venice SD35 — Cloud · Venice" 2) Prompt `a red apple on a wooden table, soft morning light`, click Generate 3) Gallery, click the new IMAGE 1:1 card
- **Expected:** a notice that the prompt goes to Venice. Known fault: the studio returns to "Ready" with no picture (REQ-UI-086; it stops waiting after 2 seconds). Gallery has the card "Venice SD35 · seed …" as a coloured tile; its details show the Venice price. 0.01 USD. Skip today: Upscale, edit, mask, inpaint, Remove background, Export
- **Covers:** REQ-UI-086, REQ-UI-094

### Owner's test, 20 Nov 2026 — 5. Video studio
- **Sign in as:** user 1
- **Steps:** 1) Video Studio › Text → Video, Model "Wan 3.0 text to video — Cloud · Venice" 2) Duration 2s, Resolution 480p, Aspect ratio 16:9, Character empty 3) type `a paper boat drifting on a calm pond, gentle ripples`, click Generate from prompt
- **Expected:** "Venice: processing · n%" and "Venice price for this clip: $0.13" for 1 to 3 minutes; then the line "Preview — clip01 (…)" and no clip plays; Gallery shows a coloured "VIDEO 0:02" card. 0.13 USD. Skip today: Image → Video, Trim, Reframe, Add audio, Split, Upscale to 1080p, Export MP4
- **Covers:** REQ-UI-089, REQ-UI-090, REQ-FN-081, REQ-FN-082

### Owner's test, 20 Nov 2026 — 6. Character and multi-shot price
- **Sign in as:** user 1
- **Steps:** 1) Characters › New Character, type `Kavya, 25, warm smile, saree, studio light`, choose Photoreal, click Create character 2) Avatar Studio › Multi-shot: Who is in the video Kavya; Shot 1 `Kavya waves at the camera`; + Add a shot; Shot 2 `Kavya smiles and turns to the window`; shortest length, lowest quality 3) click Show Venice's price
- **Expected:** a character card with a coloured tile and her initial (no picture is made); "Venice's price for 2 shots: $…". Free. Stop there: Start was not checked. Skip today: Render, Generate voice, Export MP4
- **Covers:** REQ-UI-011, REQ-FN-112

### Owner's test, 20 Nov 2026 — 7. Look only, then sign out
- **Sign in as:** user 1
- **Steps:** 1) open Voice Studio, Library, Projects and Account 2) Switch to Management: Content Policy, RunPod & Ops, Model Library 3) click Sign out, then TrStudio menu › Quit
- **Expected:** each screen opens without an error. Skip today: Voice (needs a local voice model), the digital twin (needs your pictures, a clip, a voice sample and consent), anything for ComfyUI or RunPod. Free
- **Covers:** REQ-UI-030, REQ-UI-053, REQ-UI-060, REQ-UI-106

### Register (`/register`)
- **Sign in as:** nobody; then user 1 for the email-in-use path
- **Steps:** 1) open Register 2) submit a mismatched password 3) submit user 1's email
- **Expected:** field error on mismatch; "email already in use" alert; no account is created by the test
- **Covers:** REQ-UI-002, BRD-83

### Login (`/login`)
- **Sign in as:** users 1 to 4
- **Steps:** 1) open the app signed out 2) sign in with "Remember me on this computer for 30 days" ticked, note the landing screen 3) quit and reopen 4) sign out, reopen 5) sign in unticked, quit and reopen
- **Expected:** each role lands as in the table above; bad credentials show a danger alert; step 3 opens signed in; steps 4 and 5 ask for sign-in; a refused or expired kept sign-in shows "Please sign in again" with a plain message
- **Covers:** REQ-UI-001, REQ-UI-105, REQ-UI-106, REQ-FN-012, REQ-FN-016, REQ-FN-100, REQ-FN-101, BRD-57, BRD-84

### Forgot password (`/forgot-password`)
- **Sign in as:** nobody
- **Steps:** 1) open Forgot password 2) submit an email
- **Expected:** the same confirmation for any email (no hint whether it exists); the reset itself is AppManager's
- **Covers:** REQ-UI-003, BRD-57

### Setup required (`/setup-required`)
- **Sign in as:** user 2 before setup, then user 1
- **Steps:** 1) sign in as user 2 2) sign in as user 1, enable a source and install a model 3) sign in as user 2 again
- **Expected:** user 2 first sees only Setup required; after setup lands on `/avatar`
- **Covers:** REQ-UI-004, REQ-FN-017, BRD-85

### Admin — Hardware & setup (`/admin/setup`)
- **Sign in as:** user 3
- **Steps:** 1) open `/admin/setup/hardware` 2) open `/admin/setup` 3) run a setup step
- **Expected:** live GPU, CPU, memory, MPS and disk with a verdict; steps run (unknown step 400, running step 409); needs a host environment to finish provisioning
- **Covers:** REQ-UI-070, REQ-FN-008 to REQ-FN-010, BRD-8 to BRD-10

### Admin — Model Sources (`/admin/sources`)
- **Sign in as:** user 3
- **Steps:** 1) add a Hugging Face, a Civitai and a custom source 2) press Test connection 3) reload
- **Expected:** tokens stored encrypted and never shown again; the model library lists from enabled sources
- **Covers:** REQ-UI-074, BRD-82

### Admin — Model Library (`/admin/models`)
- **Sign in as:** user 3
- **Steps:** 1) search 2) install a model 3) pause, resume, cancel
- **Expected:** size, memory and licence shown; the install queue follows each action; needs a reachable model to download
- **Covers:** REQ-UI-076, REQ-FN-011, BRD-11

### Admin — Model & Provider Registry (`/admin/registry`)
- **Sign in as:** user 3
- **Steps:** 1) add a registry row with a parameters schema 2) open Image studio 3) on the providers panel add a HeyGen or aggregator key
- **Expected:** the studio renders the new row's controls with no code change; keys are masked on read (`****2345`); edits persist across reload
- **Covers:** REQ-UI-071, REQ-FN-006, REQ-FN-007, BRD-6, BRD-7, BRD-73, BRD-77

### Admin — ComfyUI import & debug (`/admin/comfy/import`)
- **Sign in as:** user 3
- **Steps:** 1) import API-format workflow JSON (invalid gives 400) 2) map inputs and save as a model 3) try to enable it before a passing smoke
- **Expected:** the graph is hidden; the sandbox gate gives 409, then 200 after the smoke; versions pin; a live run needs `ComfyUI:BaseUrl`
- **Covers:** REQ-UI-072, BRD-34 to BRD-41

### Admin — RunPod & ops (`/admin/ops`)
- **Sign in as:** user 3
- **Steps:** 1) register a RunPod endpoint 2) set a monthly cap 3) submit a job over the cap
- **Expected:** spend tracked across RunPod and closed APIs; over cap gives 402 and a near-cap alert; a non-admin gets 403; a cold start needs a RunPod key
- **Covers:** REQ-UI-073, REQ-FN-028, BRD-12, BRD-13, BRD-15, BRD-75

### Admin — Content policy (NSFW) (`/admin/policy`)
- **Sign in as:** user 1 in personal mode
- **Steps:** 1) switch Unrestricted on 2) confirm 3) submit a minor or public-figure prompt
- **Expected:** the switch moves only after confirmation; synthetic adult content allowed; the floors still block with 403; the switch is forced off in multi-user mode
- **Covers:** REQ-UI-075, BRD-79, BRD-80

### Avatar studio (`/avatar`)
- **Sign in as:** user 1
- **Steps:** 1) pick a character and a voice 2) write a script, optionally Script-assist 3) pick Fast, High or Premium and Render
- **Expected:** the cost estimate shows first; one control routes to three providers (Premium shows a HeyGen badge); captions give valid SRT; preview is a placeholder until lip-sync models or a HeyGen key are supplied
- **Covers:** REQ-UI-010, REQ-FN-037, REQ-FN-039, BRD-27 to BRD-33, BRD-53, BRD-63

### Character library (`/avatar/characters`)
- **Sign in as:** user 1
- **Steps:** 1) New Character 2) fill the prompt, choose Photoreal or Stylized, Create character 3) open the card's Character Bible
- **Expected:** the card appears only on success and persists for this user; the bible loads its entries; the realism flag feeds the safety floor
- **Covers:** REQ-UI-011, REQ-RAG-004, BRD-27, BRD-55

### Image studio (`/image`)
- **Sign in as:** user 2
- **Steps:** 1) pick a model and type a prompt 2) Generate 3) generate again with the same seed
- **Expected:** parameters render from the registry schema; tiles carry a seed badge; the same seed reproduces a byte-identical result; real pixels need an installed ONNX model. Venice rows (phase 2) carry a "Cloud · Venice" badge
- **Covers:** REQ-UI-020, REQ-FN-019 to REQ-FN-023, BRD-16 to BRD-21

### Voice studio (`/voice`)
- **Sign in as:** user 2
- **Steps:** 1) pick language and voice, type text, Generate voice 2) Clone a voice with a sample, without ticking "synthetic"
- **Expected:** English and Hindi behind one control; the real-person clone is blocked (403) until consent is recorded; real audio needs a voice model
- **Covers:** REQ-UI-030, REQ-FN-031 to REQ-FN-034, BRD-22 to BRD-26, BRD-62

### Video studio (`/video`)
- **Sign in as:** user 2
- **Steps:** 1) choose Image to Video (first and last frames) or Text to Video 2) set duration, motion, aspect and tier 3) Generate, then trim, join and export
- **Expected:** a foreign asset as a frame gives 400; a placeholder clip appears; generation needs a video model or key, export needs ffmpeg
- **Covers:** REQ-UI-040, REQ-FN-053, BRD-42 to BRD-46

### Prompt studio (`/prompt-studio`)
- **Sign in as:** user 1
- **Steps:** 1) New chat, pick a model (seed: GPT-4o with vision, Llama-3 text only) 2) Send a message 3) Copy, Save to Library, Send to Studio
- **Expected:** sessions persist per user (another user gets 404); a picture on a text-only model gives 400; without a configured model the reply is labelled synthetic
- **Covers:** REQ-UI-081, REQ-UI-082, REQ-RAG-005, BRD-88 to BRD-92

### Gallery (`/gallery`)
- **Sign in as:** user 2, then user 4
- **Steps:** 1) filter the grid 2) open an asset's details and Reproduce 3) as user 4, open user 2's asset address
- **Expected:** only the signed-in user's assets; reproduce reuses every parameter; user 4 gets 404
- **Covers:** REQ-UI-050, BRD-47, BRD-51, BRD-81

### Projects (`/projects`)
- **Sign in as:** user 2
- **Steps:** 1) open a project 2) select a run in the history 3) Reproduce with these params
- **Expected:** run parameters show prompt, model, tier, seed and cost; the run reproduces in one click
- **Covers:** REQ-UI-051, BRD-48

### Comparison (`/compare`)
- **Sign in as:** user 2
- **Steps:** 1) open two assets from Gallery's Compare 2) switch side-by-side and overlay
- **Expected:** a parameter table highlights the differing values ("N differ")
- **Covers:** REQ-UI-052, BRD-49

### Library (`/library`)
- **Sign in as:** user 2
- **Steps:** 1) save a prompt, style and voice preset 2) reload 3) Use in Studio
- **Expected:** all four preset types save (201) and persist; Use in Studio opens the right studio pre-filled
- **Covers:** REQ-UI-053, BRD-50

### Account & spend (`/account`)
- **Sign in as:** user 4
- **Steps:** 1) open Account 2) compare the spend cards with recent jobs
- **Expected:** spend by channel against the cap; licence state from AppManager
- **Covers:** REQ-UI-060, BRD-52, BRD-58

## Automated tests

```
bash .tfcore/utils/tf-build.sh test
dotnet test tests/integration/TrStudio.Tests.Integration --filter Category=Live
bash .tfcore/utils/tf-verify-boot.sh start --head web --project tests/TrStudio.TestHost/TrStudio.TestHost.csproj --port 5099
npx playwright test
```
The first line runs the unit tests (497 pass, 18 skipped on 15 Nov 2026) and the integration tests. The paid Venice live tests (`Category=Live`) are left out by default; the second line runs only them, and each is skipped without a Venice key. `tests/TrStudio.TestHost` is a test-only web host (never shipped) serving the same screens and engine on a temporary SQLite folder; the third line boots it, and the Playwright specs in `tests/verify/` run against http://localhost:5099 (phase 3 checks: 10 pass, 1 skipped, the real AppManager sign-in, which needs AppManager values). Test settings come only from JSON files beside the tests: `tests/verify/testsettings.json` overlaid by the git-ignored `tests/verify/testsettings.Local.json` (AppManager values for the real sign-in check, test users), and `tests/integration/TrStudio.Tests.Integration/testsettings.json` overlaid by the git-ignored `testsettings.Local.json` (`Venice:ApiKey`); never environment variables.

## Known limitations

- What still shows a placeholder instead of a real result (checked 20 Nov 2026): Gallery, Projects and Comparison draw every picture and clip as a coloured tile; no clip or audio plays anywhere in the app; Video studio's preview is a line of text; Character library cards are a coloured tile with an initial (creating a character makes no picture); Avatar studio's Render, Voice studio and Avatar's Generate voice give made-up results; Trim, Reframe, Add audio, Split, Upscale to 1080p, Export MP4 or WAV, Remove background, picture Upscale, Gallery's Reproduce, Projects' Rename and Account's Upgrade only show a message.
- Real results through Venice today: Prompt studio replies, pictures, 2-second and longer clips, multi-shot prices, joined clips; each is saved and priced, and shows only as a coloured tile (above).
- Image studio shows no picture from a real Venice model: it stops waiting after 2 seconds; the picture is saved to Gallery (REQ-UI-086, found 20 Nov 2026, next `*fix-issues TrStudio REQ-UI-086`).
- Picture edit (image to image) and Video studio's Image → Video were not checked on real Venice; the code read of 20 Nov 2026 suggests the reference picture and the prompt are not sent.
- Needs a model or service this Mac does not have: ComfyUI runs (REQ-FN-045 to REQ-FN-047), RunPod (REQ-FN-028), HeyGen and fal.ai, local ONNX pictures, local voice, lip-sync and Whisper captions (moved to a later version 20 Nov 2026: REQ-FN-031, REQ-FN-037, REQ-FN-038, REQ-RAG-002, joining REQ-FN-117).
- Premium tier gating (REQ-FN-014) needs a Premium licence on user 4.
- Data residency and retention (REQ-NFR-010) are deployment settings.
- Not built (could-haves): webhooks and the MCP server; their tests wait for the feature. The digital twin, multi-shot assembly and making a clip longer are built on Venice (17 Nov 2026); clips are joined in the Mac app with Apple's AVFoundation. The digital twin is off until the owner turns it on in Content policy, and needs the recorded consent of the person shown.
- Every studio needs at least one enabled model source and one ready registry model; the development seed provides `stub-echo` and `onnx-sdxl-local`.
- Libraries: TrBlazeUI 2.2.5 and TechieRag 1.1.5 from nuget.org. TrBlazeUI TR-015 and TR-016 are open and block nothing; TechieRag TR-RAG-002 to TR-RAG-006 are open and none blocks; TechieFlow TF-011 blocks the mockup check of four Video studio rows.
- Each screen's sample data for the checks comes from `tests/verify/seed/<screen>.sh`, run against the test host (decision 11); a fresh app shows its own, mostly empty, data instead.

## Platform notes

**macOS (Apple Silicon), first version:** use the command line, VS Code or Rider. The app needs full Xcode (`sudo xcode-select -s /Applications/Xcode.app/Contents/Developer`, licence accepted). The csproj sets `ValidateXcodeVersion=false` while the Mac Catalyst workload (26.5) trails Xcode (27.2); `sudo dotnet workload update` lets it be removed. Zoom with Cmd+ / Cmd− / Cmd0 (the page is laid out again at each size and stays centred, fixed 15 Nov 2026) after clicking inside the window; the level persists and scales up on high-density displays. A kept "Remember me" sign-in is encrypted with the Keychain-held key; AppManager may end it sooner than 30 days.

**Signing the Mac app (decision 15):** every Debug build is signed with the owner's free Personal Team: certificate "Apple Development: S Ravi Kumar" and the profile "Mac Catalyst Team Provisioning Profile: com.techierathore.trstudio". The signature carries the Keychain permission the app needs. The profile ends on 17 Nov 2027 and the certificate on **4 Oct 2027**, so signing ends on 4 Oct 2027. On that day a new build stops at the signing step with an expired or missing certificate, and from then (at the latest from 17 Nov 2027) the built app may refuse to open ("TrStudio quit unexpectedly" or "can't be opened"). Your library is not touched. To make a new certificate and profile, run on this Mac:

```
bash tools/mac-signing/renew-mac-profile.sh
```

Then build as usual; it prints the new end dates. If it says the Apple ID needs signing in, sign in under Xcode › Settings › Accounts and run it again. To see the app's Keychain items (names and dates only, never a value): `bash tools/mac-signing/keychain-list.sh`.

**Windows (later version):** the Windows head stays in the project; on a Windows machine, `dotnet build src/TrStudio/TrStudio.csproj -f net10.0-windows10.0.19041.0`. No step of the first version needs Windows.

**Checking the real Mac app with the framework's driver:** Appium's mac2 driver needs macOS Automation Mode on without a password prompt (`sudo automationmodetool enable-automationmode-without-authentication`, done on this Mac by 18 Nov 2026). `tf-verify-boot.sh start --head maccatalyst --project src/TrStudio/TrStudio.csproj` starts Appium and the app; `node tests/verify/mac-real-app.mjs <step>` signs in, signs out, quits, opens, clicks (`scrollclick` scrolls a control into view first) and takes window screenshots.

**Troubleshooting:** The Mac app quits the moment it starts, with a crash in `load_aot_module` → old build output; delete `src/TrStudio/bin` and `src/TrStudio/obj` and build again. `NU1101: TechieRag` or `NU1301` → `nuget.config` missing or token invalid. `requires Xcode X.Y` → `sudo dotnet workload update`. Login says the copy was built without its sign-in settings → fill in `src/TrStudio/appmanager.local.json` and rebuild. Login shows "Please sign in again" → the kept sign-in ran past 30 days or AppManager refused it; sign in again. A screen errors at start → read the log under the app data folder's `TrStudio/logs`.
