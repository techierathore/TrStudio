# TrStudio — Usage Guide (Run · Test Users · UAT Plan)

> How to **run and test** TrStudio on Windows and macOS. Agents (smoke/verify) and human UAT use the SAME test users and walkthrough below (enforced by `.tfcore/tasks/_smoke-test-policy.md`).
>
> **Looking for an end-user manual to share with customers?** That is the **Product Guide** (`*productguide TrStudio` generates it — screenshot-illustrated, task-oriented, no internal test details). This document is the internal run/test reference.

## Quick start

| I want to… | Do this |
|---|---|
| Run the app on **Windows** | [Windows · Visual Studio](#how-to-run-windows-visual-studio) or [Windows · CLI](#how-to-run-windows-command-line) |
| Run the app on **macOS** | [macOS](#how-to-run-macos-apple-silicon) (one-time setup, then two commands) |
| Run the **native Mac desktop app** | [MAUI desktop head (Mac Catalyst)](#run-the-maui-desktop-head-mac-catalyst) |
| Test it | Log in with a [test user](#test-users) and follow the [UAT plan](#uat-plan-screen-by-screen) or the [smoke checklist](#smoke-checklist-quick-capability-pass) |

## Test users

Identity is delegated to **AppManager**; TrStudio stores only the AppManager `userId` — no local passwords. Real AppManager login is the primary path; the dev seam (`/dev/session`, needs `AllowDevSymmetricTokens=true`) remains for role-switching smokes.

| # | Email | Password | Role | Exists? |
|---|-------|----------|------|---------|
| 1 | admin@appmanager.local | `Admin@123!` | Superadmin / Owner (Creator + Operator, all tiers) | ✅ pre-existing — never create or overwrite |
| 2 | creator@trstudio.test | `TrStudio!Creator1` | Standard creator (Fast + High) | ✅ |
| 3 | admin@trstudio.test | `TrStudio!Admin1` | Operator / Admin | ⬜ create in AppManager when needed |
| 4 | premium@trstudio.test | `TrStudio!Premium1` | Premium creator | ⬜ register, then assign a Premium license in AppManager (exercises FN-014) |

## Who lands where

Per BRD-84 (role landing) and BRD-85 (first-run setup gate): on every login the app checks *≥1 enabled model source AND ≥1 ready registry model*; until then it is un-set-up.

| Role | Lands on | While setup is incomplete |
|------|----------|---------------------------|
| Superadmin / Owner (1) | Avatar studio `/avatar`; switches to the management surface via the sidebar footer link | Guided setup `/admin/setup` |
| Operator / Admin (3) | Ops dashboard `/admin/ops` | Hardware & readiness `/admin/setup/hardware` → guided setup |
| Creators (2, 4) | Avatar studio `/avatar` | `/setup-required` — "ask your admin" |

- **Creator surface**: Avatar studio → Characters · Image · Video · Voice · Prompt studios → Gallery · Projects · Account (Comparison + Library open from the Gallery toolbar).
- **Management surface** (`/admin/*`, role-gated): Hardware & Setup · RunPod & Ops · Model Sources · Model Library · Registry & Providers · ComfyUI Import · Content Policy.
- The **Owner** sees both and an in-app switch (sidebar footer).

## Prerequisites

- **.NET 10 SDK** (+ the **.NET MAUI workload** for the desktop head — `dotnet workload list` should show `maui`)
- **The two dependency services** — PostgreSQL+PgVector and AppManager. Run them as local containers, **or** point at another machine's containers over the LAN (see the macOS section). If either is down, startup/login fails.
- **Two gitignored secrets files** — recreate on every new machine (they never travel with the source):
  - `nuget.config` ← from `nuget.config.template` + your GitHub PAT (`read:packages`). Without it: `NU1101: TechieRag`.
  - `src/TrStudioApi/appsettings.json` ← from `appsettings.template.json` + DB connection string + the `AppManager` block.

## Which projects to run

Start **`TrStudioApi` first** (backend, http://localhost:5102 — `/swagger`, `/health`), then ONE UI head:

| Head | Project | Where |
|------|---------|-------|
| **Web** | `src/TrStudio` (Blazor Web) | http://localhost:5099 |
| **Desktop** | `src/TrStudio.Desktop` (MAUI Blazor Hybrid — Windows + Mac Catalyst) | native window |

Everything else in `TrStudio.slnx` is a library (`TrStudio.Engine`, `TrStudio.Models`, `TrStudioUI` RCL, `TrStudioDb`). Both heads render the same `TrStudioUI` screens and are feature-identical.

## How to run — Windows · Visual Studio

1. Start the dependency containers and confirm both are up:
   ```powershell
   docker start WinPgVectorDb appmanager-api
   docker ps        # expect both "Up" (5432 and 5101)
   ```
2. Open `TrStudio.slnx` (VS 2022 17.10+). Solution → **Configure Startup Projects… → Multiple startup projects**: `TrStudioApi` + `TrStudio` = Start (API on top). For the desktop app use `TrStudioApi` + `TrStudio.Desktop` instead (framework `net10.0-windows10.0.19041.0`).
3. F5 → wait for the API console to log `Application started`, then open **http://localhost:5099** and log in as a test user.

## How to run — Windows · command line

```powershell
docker start WinPgVectorDb appmanager-api
dotnet run --project src/TrStudioApi   --urls http://localhost:5102   # terminal 1 — wait for "Application started"
dotnet run --project src/TrStudio      --urls http://localhost:5099   # terminal 2
# desktop head instead of web: dotnet build src/TrStudio.Desktop -f net10.0-windows10.0.19041.0
```

## How to run — macOS (Apple Silicon)

### One-time setup (new Mac)

1. **Secrets** — `cp nuget.config.template nuget.config` and `cp src/TrStudioApi/appsettings.template.json src/TrStudioApi/appsettings.json`, then fill both in.
2. **Dependency services** — either:
   - **Local containers**: install Docker Desktop / OrbStack; run a PgVector Postgres (`docker run -d --name MacPgVectorDb -p 5432:5432 -e POSTGRES_USER=… -e POSTGRES_PASSWORD=… -e POSTGRES_DB=trstudio -v macpgvector-data:/var/lib/postgresql/data pgvector/pgvector:pg16`) and your `appmanager-api` image on :5101 (keep its `/keys` volume writable); **or**
   - **Reuse the Windows box's containers over the LAN** (no local Docker): point `ConnectionStrings:Default` at `Host=<windows-ip>;Port=5432;…` directly, and keep `AppManager:BaseUrl` = `https://localhost:5101/` bridged by the loopback relay — `python3 tools/appmanager-forward.py &` (edit the target IP inside if it changes). The BaseUrl **must stay localhost**: the self-signed-cert relaxation is loopback-only by design (NFR-05). Trade-off: the Windows machine must be on.
3. **(Desktop head only) Full Xcode** — Command Line Tools are not enough: install Xcode, `sudo xcode-select -s /Applications/Xcode.app/Contents/Developer`, accept the licence.

> No Visual Studio on the Mac — use the CLI, VS Code, or Rider.

### Run the web app (Mac CLI)

```bash
python3 tools/appmanager-forward.py &                          # only for the LAN-containers setup
dotnet restore TrStudio.slnx -p:EnableWindowsTargeting=true    # flag: the Desktop head also targets Windows
dotnet run --project src/TrStudioApi   --urls http://localhost:5102   # terminal 1 — wait for "Application started"
dotnet run --project src/TrStudio      --urls http://localhost:5099   # terminal 2
```
Open http://localhost:5099 and log in as a test user.

### Run the MAUI desktop head (Mac Catalyst)

```bash
dotnet build src/TrStudio.Desktop -f net10.0-maccatalyst -c Release -p:EnableWindowsTargeting=true -p:ValidateXcodeVersion=false
open src/TrStudio.Desktop/bin/Release/net10.0-maccatalyst/maccatalyst-arm64/TrStudio.app
```

- `-p:ValidateXcodeVersion=false` is needed only while the installed MacCatalyst workload band trails your Xcode by a minor version (`sudo dotnet workload update` removes the need once a matching band ships).
- Start `TrStudioApi` first; point the app elsewhere with `TrStudioApiBaseUrl=http://<host>:5102`.
- Zoom the whole UI with **⌘+ / ⌘− / ⌘0** (in / out / actual size) — click once inside the window first so it has focus. The level persists across restarts, and the default auto-scales up on high-DPI / native-4K displays so text is readable out of the box.
- Requires TrBlazeUI ≥ 1.0.9 (1.0.7 could not build for Catalyst — TR-011, fixed).

### Mac troubleshooting

- `NETSDK1100 …targeting Windows` on solution-level commands → add `-p:EnableWindowsTargeting=true`.
- `NU1101: TechieRag` → `nuget.config` missing or PAT invalid.
- `requires Xcode X.Y (current X.Z)` → `sudo dotnet workload update`, or build with `-p:ValidateXcodeVersion=false`.
- Login 503 `APPMANAGER_UNREACHABLE` / "SSL connection could not be established" → `AppManager:BaseUrl` points at a LAN IP; use `https://localhost:5101/` + `tools/appmanager-forward.py` (NFR-05 loopback rule).
- Login HTTP 500 / `UPSTREAM_ERROR` → the AppManager container's `/keys` volume isn't writable.

## Troubleshooting startup (both platforms)

- **DB/login errors at launch** → a dependency service is down (`docker ps`) or only one project was started. The shell header shows an **API status pill**; the API logs `Verify PostgreSQL is running…` when it can't reach Postgres.
- **App loads but actions fail / data won't save** → the API isn't on :5102 — check `http://localhost:5102/health` (expect `{"status":"ok"}`) and the header pill.

## Test (automated)

```bash
dotnet test        # 260 tests (259 unit + 1 SignalR integration)
```
Headless Playwright verify specs: `tests/verify/` — `npx playwright test` against a running stack (API :5102 + web :5099).

## UAT plan — screen by screen

> Internal acceptance script (one entry per screen: user → steps → expected → BRD coverage). Screens degrade cleanly where a model/key/service isn't supplied — those spots are marked ⚠.

### Register (`/register`) — covers BRD-83
As nobody: register a new account → created via the AppManager API (no local password), redirect to login.

### Login (`/login`) — covers BRD-57, BRD-84
As users 1–4: log in → JWT session via AppManager; each role lands per the table above; first run routes to the setup gate. Signed-out visitors are redirected to `/login` from any screen.

### Forgot password (`/forgot-password`) — covers BRD-57
Submit an email → enumeration-safe confirmation; reset handled by AppManager.

### First-run gate & Setup-required (`/setup-required`) — covers BRD-85
User 2 before setup → `/setup-required` only. User 1 → guided setup; enable a source + install a model; user 2 then lands on `/avatar`.

### Admin — Hardware & readiness (`/admin/setup/hardware`) — covers BRD-8
User 3: readiness summary reflects live host capabilities (GPU/CPU/memory/MPS/disk) with a verdict.

### Admin — Guided setup (`/admin/setup`) — covers BRD-9, BRD-10
User 3: wizard drives ONNX Runtime install + isolated Python/ComfyUI provisioning (400 unknown / 409 already-running). ⚠ real provisioning needs a host GPU/env.

### Admin — Model sources (`/admin/sources`) — covers BRD-82
User 3: add Hugging Face / Civitai / custom-URL sources → tokens stored encrypted (never echoed), Test connection works, Model library lists from enabled sources.

### Admin — Model library (`/admin/models`) — covers BRD-11
User 3: browse + one-click install with pause/resume/cancel; size/VRAM/licence shown. ⚠ real download needs a reachable model.

### Admin — Registry (`/admin/registry`) — covers BRD-6, BRD-77
User 3: add a registry row with a params schema → the model is usable from the studios with **controls auto-rendered from the schema** (no code change).

### Admin — Providers (`/admin/providers`) — covers BRD-7, BRD-73
User 3: add HeyGen/aggregator keys → encrypted at rest, masked on read (`****2345`), selectable per model.

### Admin — RunPod & cost (`/admin/ops`) — covers BRD-12/13/15/75
User 3: register a RunPod endpoint; spend tracked across RunPod + closed APIs; over-cap → HTTP 402; near-cap alert; non-admin → 403. ⚠ live cold-start needs `RunPod:ApiKey`.

### Admin — ComfyUI import (`/admin/comfy/import`, `/admin/comfy/debug`) — covers BRD-34..41
User 3: import API-format workflow JSON (invalid → 400) → map inputs → save-as-model **hides the graph** → sandbox gate blocks go-live before a passing smoke (409→200) → version-pin works. ⚠ live run needs `ComfyUI:BaseUrl`.

### Admin — Content policy / NSFW (`/admin/policy`) — covers BRD-79, BRD-80
User 1 (owner, personal mode): the Unrestricted toggle sits behind a confirmation gate; synthetic-adult allowed; real-person/photoreal-of-real refused; **CSAM + minor + public-figure floors still fire (403)**; forced off in multi-user mode.

### Image studio (`/image`, `/image/batch`) — covers BRD-16..21
User 2: prompt → generate → I2I/inpaint → ControlNet → upscale → seed grid → 9:16 export with background removal. Params render from the registry schema; same seed reproduces **byte-identical**. ⚠ real pixels need an installed ONNX model.

### Voice studio (`/voice`, `/voice/clone`) — covers BRD-22..26, BRD-62
User 2: EN + HI generation behind one language control; synthetic-voice clone ungated; **real-person clone blocked until consent is recorded** (403). ⚠ real synthesis needs a voice model.

### Avatar studio (`/avatar`, `/avatar/characters`) — covers BRD-27..31/33/53/55/63
User 1: create a photoreal character (identity lock) → script → voice → animate at Fast/High/Premium (**one control, 3 providers**) → captions (valid SRT) → export with provenance. Realism flag feeds the safety floor. ⚠ live lip-sync/HeyGen needs models/keys.

### Video studio (`/video`, `/video/edit`) — covers BRD-42..46
User 2: I2V/T2V with duration/motion/aspect; first/last-frame conditioning (foreign asset → 400); interpolate/upscale; trim/join + audio; MP4 export at 16:9/9:16/1:1/4:5. ⚠ generation needs a video model/key; real export needs ffmpeg.

### Prompt studio (`/prompt-studio`) — covers BRD-88..92
User 1: pick a `text-chat` LLM (admin-configured on `/admin/registry`; dev-seed: GPT-4o vision + Llama-3 text-only) → chat to craft a prompt → image attach only on vision-capable models (non-vision + image → 400) → Copy / Save-to-Library / Send-to-Studio → sessions persist per user (cross-user → 404); tokens accrue to `SpendChannel.LlmText`. ⚠ without a configured LLM it degrades to a labeled synthetic reply.

### Workspace (`/gallery`, `/projects`, `/compare`, `/library`, `/account`) — covers BRD-47..52, BRD-58
User 2 (+4 for spend/license): reuse assets, one-click reproduce, param-diff comparison, presets, bulk export with provenance headers, spend by channel + license state.

### Safety floors — covers BRD-60/61/64/65
User 1: blocked-class input → **403 before enqueue** + audit-logged; public-figure impersonation blocked by default; every job (allowed + blocked) in the audit trail.

### Per-user isolation — covers BRD-81
User 2 creates an asset → user 4 cannot see or fetch it (server-side filter; cross-user GET → 404).

## How-to walkthroughs (creator flows)

Step-by-step click paths for the main creation tasks, using the on-screen labels exactly as they appear. Do these signed in as the **Owner** (`admin@appmanager.local`) — the Owner reaches every studio directly.

> **Read this first — what "Generate" actually shows today.** These flows are wired end-to-end (real jobs, real seeds, real per-user persistence, real safety/consent gates), **but the studios do not yet paint the finished pixels/audio/video on screen.** On this build each studio animates a progress bar, runs the job, then shows a **placeholder preview** — a gradient image tile (with its real **seed** badge), a placeholder waveform, or a placeholder timeline clip — plus a green success toast. When no real model/key/host is configured, the backend also returns a **synthetic-dev** asset rather than a real render. So a successful UAT pass here means *"the flow, controls, routing, cost, safety and persistence all work"* — not *"a photo of my prompt appeared."* Real outputs light up when the corresponding model/key/host is supplied (see **Known limitations**). Buttons labelled **Upscale**, **Export MP4 / WAV**, and **Remove background** are UI stubs today (they toast, they don't produce a file).
>
> **Prerequisite for every studio:** at least one **enabled** model source (`/admin/sources`) **and** one ready registry model. A plain Creator with neither is held on `/setup-required`; the Owner is not gated but still needs a ready model for the picker (the dev seed provides `stub-echo` / `onnx-sdxl-local`).

### A. Create an image from text (text → image) — `/image`
1. Open **Image studio** (left nav → Studio, or go to `/image`).
2. In the left **Model** card, open **Select a model** and pick a text-to-image model (e.g. the seeded SDXL row). The **Parameters** card now auto-renders that model's controls.
3. In the center **Canvas**, type your prompt into the **Prompt** box. Optionally set **Negative prompt**, **Width/Height**, **Steps**, **Guidance scale** in Parameters, and leave **Seed** empty ("Random when empty") for a fresh seed.
4. Click **Generate**.
5. **What you'll see:** a progress bar, then result tile(s) each stamped with a `#seed` badge and a toast like *"3 result(s) ready — seeds …"*. Re-entering the **same seed** and generating again reproduces a byte-identical result (this is the reproducibility check, FN-023).

### B. Edit / transform an image from a prompt (img2img · inpaint · ControlNet) — `/image`
1. On **Image studio**, upload your starting image in the **Conditioning (ControlNet)** card's reference upload, and pick a control tab — **Pose**, **Depth**, or **Edges**.
2. In the **Canvas** toolbar choose an edit tool — **Brush**, **Mask**, **Inpaint**, or **Erase** (these set the active edit mode).
3. Adjust the **Prompt** to describe the change, then click **Generate**.
4. **What you'll see:** same placeholder-tile behaviour as flow A — the source/mask/control settings are carried into the job (uploads go to `/api/v1/images/uploads`, owner-scoped), but the rendered preview is synthetic until real ControlNet weights/model are installed.

### C. Create an avatar (character → talking-head) — `/avatar/characters` then `/avatar`
**Step 1 — make the character** (`/avatar/characters`):
1. Click **New Character** (or the empty-state **Create a character**).
2. In the **New Character** dialog fill **Image-gen prompt**, pick **Realism** → **Photoreal** or **Stylized**, optionally set **Seed lock** and **LoRA** (only **None** is wired today).
3. Click **Create character** — the card is added only on success (it persists, owner-scoped). Click the card to open its **Character Bible**.

**Step 2 — render the avatar** (`/avatar`, the default landing):
1. In the left **Character** card pick your character (or **Change character**).
2. In the **Voice** card choose a **Preset voice** and **Language** (EN/HI); click **Generate voice** to preview.
3. Type the spoken lines in the **Script** box; optionally click **Script-assist (TechieRag)**.
4. Choose a **Render tier** — **Fast**, **High**, or **Premium** (Premium shows a **HeyGen** badge + a "Routed to HeyGen (closed API)" notice; needs a HeyGen key + Premium licence).
5. Check the **Cost estimate**, then click **Render**. Optionally set **Burn-in captions** / **Aspect ratio** and **Export MP4**.
6. **What you'll see:** live character/voice/spend data; render shows progress then a **playable placeholder** preview + success toast (no real talking-head video yet — lip-sync/HeyGen are model/key-gated). A safety-blocked script surfaces the SAFE floor alert. Captions produce a valid SRT.

### D. Generate speech (text → voice) — `/voice`
1. Open **Voice studio**. Pick a **Language** (English/Hindi/Hinglish) and, if enabled for the voice, an **Emotion / style**.
2. Open the **Voice picker** and select a voice (a ready voice shows a **ready** badge; a **no model** badge means it can't synthesize).
3. Type into **Text to speak**, adjust **Speed**/**Pitch** if supported, then click **Generate voice**.
4. **What you'll see:** progress → a placeholder **waveform** marked playable + a success (or "degraded mode") toast; real audio playback and **Export WAV** are stubs until a voice model is installed. To test the consent gate: **Clone a voice** → upload a sample → *without* ticking "synthetic/character voice" you must pass **I confirm recorded consent** or it's blocked (403).

### E. Generate video (image→video / text→video) — `/video`
1. Open **Video studio**. Choose the **Mode** tab — **Image → Video** (upload First/Last **Conditioning frames**) or **Text → Video** (fill the **Prompt**).
2. Set **Duration**, **Motion strength**, **Aspect ratio**, and a **Quality tier** (Fast/High/Premium — the cloud provider is shown as a badge).
3. Click **Generate from frame** / **Generate from prompt**.
4. **What you'll see:** progress → a **placeholder timeline clip** + success toast. Edit tools (**Trim/Join/Reframe/Split**), **Interpolate**, **Upscale to 1080p**, and **Export MP4** are best-effort/stub — real generation needs a video model/key and real export needs ffmpeg on the host.

### F. Craft a prompt with AI, then send it to a studio — `/prompt-studio`
1. Open **Prompt studio**. Start a **New chat**; pick a **Model** (dev seed: **GPT-4o** vision, **Llama-3** text-only). A badge shows whether image attach is on.
2. Type what you want in the composer and click **Send** (attaching an image to a text-only model is correctly rejected).
3. On a reply use **Copy**, **Save to Library**, or **Send to Studio → Image / Avatar / Video Studio** (this opens that studio pre-filled with the prompt).
4. **What you'll see:** a real per-user chat (sessions persist, cross-user blocked). Without a configured LLM the reply is a labelled `[DEV SYNTHETIC PROMPT-CHAT]` + a "Degraded" toast; token usage still accrues.

## Smoke checklist (quick capability pass)

- [ ] Signed-out visit to any screen redirects to `/login`; each role lands on its default page (BRD-84); fresh install routes to setup (BRD-85)
- [ ] API status pill in the header reflects the backend (stop the API → pill goes red)
- [ ] Admin: hardware readiness renders live host capabilities
- [ ] Creator: generate + reproduce one image (byte-identical seed)
- [ ] Creator: EN + HI speech; real-person clone is consent-gated
- [ ] Owner: one avatar spec routes across Fast / High / Premium
- [ ] Admin: ComfyUI import → save-as-model (graph hidden) → sandbox gate 409 before smoke
- [ ] A safety floor fires (403 before enqueue) + audit trail + provenance on export
- [ ] Prompt studio: crafted prompt back; Copy / Save-to-Library / Send-to-Studio; image on a text-only model blocked
- [ ] Second user cannot see the first user's assets

## Known limitations

- **Model/service-gated execution** (degrades cleanly, no rebuild needed): real image pixels + provisioning (FN-009/010/011/019/021), voice synthesis (FN-031/033..035), lip-sync/HeyGen (FN-037/038), ComfyUI live run (FN-045..047), video generate/interpolate/upscale (FN-051/053), ffmpeg export (FN-054), RunPod cold-start (FN-028), TechieRag LLM paths (RAG-001/002/005). Supply the model/key/service to light each up.
- **Premium tier-gating** (FN-014) needs a Premium license assigned in AppManager (test user 4).
- **DPDP residency/retention** (NFR-010) are deployment config.
- **Deferred (Could)**: digital-twin, multi-shot, clip extension, webhooks, MCP server.
- **Library feedback**: TrBlazeUI TR-001..011 and TechieRag TR-RAG-001+3 — see `docs/TrStudio-TrBlazeUI-Feedback.md` / `docs/TrStudio-TechieRag-Feedback.md`.
