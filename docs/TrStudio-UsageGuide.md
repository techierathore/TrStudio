# TrStudio — Usage Guide

| | |
|---|---|
| App | TrStudio |
| Kind | app |
| Size | Large |
| Date | 2026-11-12 |

How to run and test TrStudio on Windows and macOS. The smoke and verify agents and the owner's acceptance testing use the same test users and the same walkthrough below. The end-user manual is the Product Guide (`*productguide TrStudio`); this is the internal run and test reference.

## Test users

Identity comes from AppManager; TrStudio stores only the AppManager `userId`, never a password. The users were registered through AppManager's register call (`node .tfcore/utils/tf-register-test-users.mjs`, run by the owner); user 1 is AppManager's own superadmin. Each password lives in the git-ignored `tests/verify/testsettings.Local.json`, laid over the committed `tests/verify/testsettings.json`; it is never written here. The developer sign-in page (`/dev/session`, needs `AllowDevSymmetricTokens=true`) stays for switching roles in smoke runs.

| # | User | Password source | Role | Exists |
|---|---|---|---|---|
| 1 | admin@appmanager.local | `TestUsers:1:Password` in the git-ignored `tests/verify/testsettings.Local.json` | Owner (superadmin: Creator and Operator, all tiers); the owner walkthrough. Never create or overwrite it | yes |
| 2 | creator@trstudio.test | `TestUsers:2:Password` in the git-ignored `tests/verify/testsettings.Local.json` | Standard creator (Fast and High); the second user, for the isolation checks | yes |
| 3 | admin@trstudio.test | `TestUsers:3:Password` in the git-ignored `tests/verify/testsettings.Local.json` | Operator; the unattended-run user for verify and smoke runs | no — register when needed |
| 4 | premium@trstudio.test | `TestUsers:4:Password` in the git-ignored `tests/verify/testsettings.Local.json` | Premium creator (exercises REQ-FN-014) | no — register, then assign a Premium licence in AppManager |

A verify test never creates, deletes or changes an account on a shared AppManager; the register-creates-an-account path is verified by hand once per release and recorded in the checklist with the date, and the automated row checks only the form's validation and the email-already-in-use path using test user 1. The Register test signs in as test user 1 before it submits anything; the test users are registered only by the owner, with `node .tfcore/utils/tf-register-test-users.mjs`.

**Who lands where** (BRD-84 role landing, BRD-85 first-run gate: setup is complete when at least one model source is enabled and at least one registry model is ready):

| Role | Lands on | While setup is incomplete |
|---|---|---|
| Owner (1) | Avatar studio `/avatar`; a sidebar-footer link switches to the management screens | Guided setup `/admin/setup` |
| Operator (3) | RunPod & ops `/admin/ops` | Hardware & readiness `/admin/setup/hardware`, then guided setup |
| Creators (2, 4) | Avatar studio `/avatar` | `/setup-required` ("ask your admin") |

## Execution guide

Prerequisites: .NET 10 SDK (plus the .NET MAUI workload for the desktop head); PostgreSQL with PgVector and AppManager running (local containers, or another machine's over the LAN); two git-ignored files recreated on every machine: `nuget.config` from `nuget.config.template` plus a GitHub token with `read:packages` (without it: `NU1101: TechieRag`), and `src/TrStudioApi/appsettings.json` from `appsettings.template.json` plus the database connection string and the `AppManager` block. Keys can go in Visual Studio user secrets instead of `appsettings.json`. Ports come from each project's `launchSettings.json` (API 5102, web 5099); no setting is read from an environment variable.

```
docker start WinPgVectorDb appmanager-api
dotnet restore TrStudio.slnx -p:EnableWindowsTargeting=true
dotnet build TrStudio.slnx -p:EnableWindowsTargeting=true
dotnet run --project src/TrStudioApi --urls http://localhost:5102
dotnet run --project src/TrStudio --urls http://localhost:5099
```

The database migrates itself (DbUp) when `TrStudioApi` starts; wait for `Application started` before starting the web head. Open http://localhost:5099 and sign in as user 1. `http://localhost:5102/health` answers `{"status":"ok"}`; `/swagger` lists the API.

In Visual Studio: open `TrStudio.slnx`, set multiple startup projects `TrStudioApi` then `TrStudio` (or `TrStudio.Desktop`, framework `net10.0-windows10.0.19041.0`), and press F5.

**Phase 2 (Venice):** sign in as user 1 or 3, open Admin — Model & Provider Registry, add provider `Venice` with address `https://api.venice.ai/api/v1` and the Venice API key (stored encrypted in the database, never in appsettings), press Test connection, then add one registry row per Venice model and task.

## How to test, screen by screen

Screens degrade cleanly where a model, key or service is missing; those steps are marked "needs".

### Register (`/register`)
- **Sign in as:** nobody; then user 1 for the email-in-use path
- **Steps:** 1) open Register 2) submit a mismatched password 3) submit user 1's email
- **Expected:** field error on mismatch; "email already in use" alert; no account is created by the test
- **Covers:** REQ-UI-002, BRD-83

### Login (`/login`)
- **Sign in as:** users 1 to 4
- **Steps:** 1) open any screen signed out 2) sign in 3) note the landing screen
- **Expected:** signed-out visits go to `/login`; each role lands as in the table above; bad credentials show a danger alert
- **Covers:** REQ-UI-001, REQ-FN-012, REQ-FN-016, BRD-57, BRD-84

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
dotnet test
dotnet test tests/integration/TrStudio.Tests.Integration --filter Category=Live
npx playwright test
```
`dotnet test` runs the unit tests and the SignalR integration test. The paid Venice live tests (`Category=Live`, one per task type) are left out of that run by default (the integration project sets `--filter Category!=Live`); the second line runs only them. The Playwright specs in `tests/verify/` need a running API on 5102 and web head on 5099. Tests read their settings from `testsettings.json` beside them, overlaid by the git-ignored `testsettings.Local.json`; never environment variables. The Venice live tests read `tests/integration/TrStudio.Tests.Integration/testsettings.Local.json` (keys `Venice:BaseUrl`, `Venice:ApiKey`, optional `Venice:Models:<Task>`; an empty model picks the cheapest listed) and each is skipped with "Venice key not set in testsettings.Local.json" when the key is empty. `Host:DotnetRoot` in the same file optionally names the .NET folder the SignalR test boots the API with.

## Known limitations

- Studios run real jobs (seeds, routing, cost, safety, per-user storage) but show placeholder previews: a gradient tile with the real seed, a placeholder waveform or timeline clip. Upscale, Export MP4 or WAV and Remove background only show a message today.
- Without a model, key or host the backend returns a synthetic development asset: image pixels and provisioning (REQ-FN-009 to REQ-FN-011, REQ-FN-019, REQ-FN-021), voice (REQ-FN-031, REQ-FN-033 to REQ-FN-035), lip-sync and HeyGen (REQ-FN-037, REQ-FN-038), ComfyUI live runs (REQ-FN-045 to REQ-FN-047), video (REQ-FN-051, REQ-FN-053), ffmpeg export (REQ-FN-054), RunPod cold start (REQ-FN-028), language-model paths (REQ-RAG-001, REQ-RAG-002, REQ-RAG-005).
- Premium tier gating (REQ-FN-014) needs a Premium licence on user 4.
- Data residency and retention (REQ-NFR-010) are deployment settings.
- Not built (could-haves): digital twin, multi-shot, clip extension, webhooks, MCP server.
- Every studio needs at least one enabled model source and one ready registry model; the development seed provides `stub-echo` and `onnx-sdxl-local`.
- Library feedback: TrBlazeUI TR-011 fixed upstream and not yet re-checked here, TR-012 open; TechieRag TR-RAG-002 to TR-RAG-004 open, none blocking.

## Platform notes

**Windows:** containers `WinPgVectorDb` (5432) and `appmanager-api` (5101); `docker ps` should show both up. Desktop head: `dotnet build src/TrStudio.Desktop -f net10.0-windows10.0.19041.0`.

**macOS (Apple Silicon):** no Visual Studio; use the command line, VS Code or Rider. Either run local containers (a `pgvector/pgvector:pg16` Postgres with database `trstudio`, and the `appmanager-api` image on 5101 with a writable `/keys` volume), or reuse the Windows machine's containers: point `ConnectionStrings:Default` at `Host=<windows-ip>;Port=5432;…` and keep `AppManager:BaseUrl` at `https://localhost:5101/`, bridged by `python3 tools/appmanager-forward.py &`. The address must stay localhost because the self-signed certificate is accepted on loopback only (NFR-005); the Windows machine must then be on. The desktop head needs full Xcode (`sudo xcode-select -s /Applications/Xcode.app/Contents/Developer`, licence accepted) and TrBlazeUI 1.0.9 or later:

```
dotnet build src/TrStudio.Desktop -f net10.0-maccatalyst -c Release -p:EnableWindowsTargeting=true -p:ValidateXcodeVersion=false
open src/TrStudio.Desktop/bin/Release/net10.0-maccatalyst/maccatalyst-arm64/TrStudio.app
```

`-p:ValidateXcodeVersion=false` is needed only while the MacCatalyst workload trails Xcode (`sudo dotnet workload update` removes it). Start `TrStudioApi` first. The desktop head still reads `TrStudioApiBaseUrl` from an environment variable to reach another host; that is an open item against the settings-file rule. Zoom with Cmd+ / Cmd− / Cmd0 after clicking inside the window; the level persists and scales up on high-density displays.

**Troubleshooting:** `NETSDK1100 …targeting Windows` → add `-p:EnableWindowsTargeting=true`. `NU1101: TechieRag` → `nuget.config` missing or token invalid. `requires Xcode X.Y` → `sudo dotnet workload update`. Login 503 `APPMANAGER_UNREACHABLE` or an SSL error → `AppManager:BaseUrl` points at a LAN address; use localhost and the relay. Login 500 `UPSTREAM_ERROR` → AppManager's `/keys` volume is not writable. Errors at launch → a container is down or only one project started; the header's API status pill turns red when the API is unreachable, and the API logs `Verify PostgreSQL is running…` when it cannot reach Postgres.
