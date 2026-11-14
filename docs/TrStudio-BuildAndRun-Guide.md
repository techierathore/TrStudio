# TrStudio — Build & Run Guide (Mac first)

> **Audience:** the owner, or anyone building and running TrStudio on a Mac. Prerequisite installs will eventually be one click in **TrSetup** (see `TrSetup/docs/TrSetup-BRD.md`); until then this guide lists them by hand.
>
> **Mac first:** the first released version of TrStudio is for Mac only. Windows (and possibly Linux) come in a later version. No step of the first version needs a Windows machine.

## 1. What TrStudio ships as

TrStudio is **one desktop app**: `src/TrStudio/TrStudio.csproj`, a .NET MAUI Blazor Hybrid app (root namespace `TrStudio`, app title TrStudio, app id `com.techierathore.trstudio`). It is a unified role-based app (ADR-012 / BRD-87): creator screens and the admin screens under `/admin/*` live in the same app, gated by role.

The engine runs inside the app. There is no API process, no web server and no database server.

| Project | Role |
|---|---|
| `src/TrStudio` | The app: native window hosting the screens and the engine |
| `src/TrStudioUI` | Every screen (Razor Class Library on TrBlazeUI) |
| `src/TrStudio.InProcess` | Runs the engine inside the app process |
| `src/TrStudio.Engine` | Jobs, providers, safety floors, cost guardrails, job runner |
| `src/TrStudio.Models` | Shared models and contracts |
| `src/TrStudioDb` | SQLite migrations |

Data lives in a local SQLite file, `TrStudio.db` (plus `TrStudio.vectors.db`), in the user's own app data folder. The app creates and migrates it on first start. Jobs run inside the app.

`src/TrStudioApi` and the PostgreSQL scripts stay on disk, out of the solution build, set aside for a later external REST API.

## 2. Build & run on the Mac

1. Prerequisites: **full Xcode** with the licence accepted (Command Line Tools alone are not enough), the .NET 10 SDK, and `sudo dotnet workload install maui`.
2. Private packages (TrBlazeUI, TechieRag) come from a private feed: copy `nuget.config.template` to `nuget.config` and fill in the token. The file is git-ignored, so every copy of the source needs it recreated.
3. Sign-in settings: copy `src/TrStudio/appmanager.template.json` to `src/TrStudio/appmanager.local.json` (git-ignored) and fill in the AppManager address, application id and API key. They are put into the build. Without them the app still opens, and Login says the copy was built without its sign-in settings.
4. Build and open:
   ```bash
   dotnet build src/TrStudio/TrStudio.csproj -f net10.0-maccatalyst
   open src/TrStudio/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/TrStudio.app
   ```
5. The whole solution builds with `dotnet build TrStudio.slnx` on a Mac. The framework's own build script is `bash .tfcore/utils/tf-build.sh` (build) and `bash .tfcore/utils/tf-build.sh test` (unit and integration tests).

The csproj sets `ValidateXcodeVersion=false` itself: this Mac has Xcode 27.2 while the Mac Catalyst workload names 26.5. A `sudo dotnet workload update` that catches the workload up lets that line be removed.

Ad-hoc signing is fine for personal use.

## 3. Settings and keys

- No setting comes from an environment variable anywhere.
- AppManager sign-in settings: `src/TrStudio/appmanager.local.json`, put into the build (§2).
- Keys a user owns (Venice, RunPod, HeyGen, closed API, text models) are entered on **Registry & providers** in the app and kept in the local database, protected by the Mac Keychain for that user. They need no file.
- **Remember me:** the Login screen has a "Remember me on this computer for 30 days" box. Ticked, the AppManager refresh token is kept on this computer, encrypted with the same Keychain-held key as the provider keys, and the next open signs in without asking, for up to 30 days from that sign-in. Not ticked, nothing is kept. Sign out always removes what was kept. AppManager may end a kept sign-in sooner than 30 days.

### Windows (later version)

The Windows head stays in the project; the csproj picks the Windows target only when building on Windows. On a Windows machine:

```powershell
dotnet build src/TrStudio/TrStudio.csproj -f net10.0-windows10.0.19041.0
```

## 4. Mac runtime prerequisites (what TrStudio needs to actually generate)

TrSetup's "app runner" role will install these; the list by hand until then:

| # | Requirement | Check | Install |
|---|---|---|---|
| 1 | ffmpeg on PATH (video export) | `ffmpeg -version` | `brew install ffmpeg` |
| 2 | Local model runtime (Fast tier) | Admin → Hardware & readiness shows green | **TrSetup installs the runtime**; model provisioning then runs in the app's admin guided setup (`/admin/*`, role-gated) |
| 3 | Keys, only for the tiers you use | Registry & providers → Test connection | Entered in the app (§3) |
| 4 | Disk space for models | — | ~400 GB free is the BRD reference for a full local library; less is fine for light use |

No database server is needed: the app creates its own SQLite files. The Mac is the natural TrStudio host (capability detection targets Apple Silicon/MPS).

## 5. First run on the Mac

1. Open `TrStudio.app` → the app creates `TrStudio.db` in your app data folder → sign in through AppManager (tick "Remember me" if you want the next opens to skip it). With setup incomplete you land in the **setup-required gate** (BRD-85), which routes admins into guided setup.
2. Complete: hardware readiness → guided setup → Model sources → install at least one model (admin screens under `/admin/*`).
3. Back in the creator screens: generate a test image (Fast tier, local MPS) → check it appears in the Gallery with provenance.

Headless checks do not need the app window: `tests/TrStudio.TestHost` is a test-only web host (never shipped) serving the same screens and engine on a temporary SQLite folder. Boot it with `bash .tfcore/utils/tf-verify-boot.sh start --head web --project tests/TrStudio.TestHost/TrStudio.TestHost.csproj --port 5099`, then run `npx playwright test` (specs in `tests/verify/`).

## 6. Where TrSetup takes over

Once TrSetup P3 ships, §4 collapses to: *run TrSetup on the Mac → role "App runner" → app "TrStudio" → Fix all*, and the Xcode and MAUI toolchain (§2) is one more Fix click. This guide stays as the manual reference.

---
*Created 2026-07-05. Rewritten 2026-11-14 for phase 3: one desktop app (`src/TrStudio`) with the engine, SQLite store and job runner inside, and no separate server process; Mac first, Windows in a later version; "Remember me" sign-in added. Full run and test steps: `docs/TrStudio-UsageGuide.md`.*
