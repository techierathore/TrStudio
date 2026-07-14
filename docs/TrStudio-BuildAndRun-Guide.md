# TrStudio — Build & Run Guide (build on Windows · run on the Mac)

<!-- AGENT NOTE (never emit as visible text): "Catalyst cannot be built from Windows" is a HOST limitation, not a deliverable cancellation. Never drop, declare impossible, or silently substitute the native Mac desktop head because your machine can't build it — §3 Option A (the owner runs ONE command on the Mac) is the standing path: prepare the source + the exact commands and hand off. Cross-publishing the plain .NET heads (§3 Option B) is a complement offered alongside, never the replacement. See build-invocation-ladder.md §"What NOT to do". -->

> **Audience:** the owner / any human moving TrStudio between the Windows dev machine and the LAN Mac. Prerequisite installs will eventually be one click in **TrSetup** (see `TrSetup/docs/TrSetup-BRD.md`); until then this guide lists them manually.
>
> **The one rule that governs everything below:** `-r osx-arm64` cross-publishes **plain .NET** projects only — the .NET runtime pack is just a NuGet download, so Windows can build a Mac binary. A **MAUI** head is different in kind: it compiles against Apple's native SDKs and is signed with Apple tools that ship only inside Xcode, **macOS only** — no flag, TFM, or RID makes a Catalyst `.app` buildable from Windows (VS "Pair to Mac" is iOS-only). So: API + web head → build on Windows, copy to the Mac (§3 Option B); the native desktop `.app` → one command **on the Mac** (§3 Option A).

## 1. What TrStudio ships as

TrStudio is now a **unified role-based app** (ADR-012 / BRD-87): one shared component set (`TrStudioUI` RCL) rendered by **two interchangeable UI heads** over one backend. The old separate `TrStudioAdmin` app is gone — admin screens live under `/admin/*` inside the same app, gated by role.

| Project | Kind | Role | How it runs |
|---|---|---|---|
| `src/TrStudioApi` | plain .NET web app (Kestrel) | Backend — jobs, registry, providers, Hangfire, JWT auth | `http://localhost:5102` |
| `src/TrStudio` | plain .NET web app (Blazor Server) | **Web head** — full app in the browser | `http://localhost:5099` |
| `src/TrStudio.Desktop` | **MAUI Blazor Hybrid** | **Desktop head** — same UI in a native window | Windows unpackaged exe · Mac Catalyst `.app` |

Because both heads host the same `TrStudioUI` RCL, they are feature-identical — the desktop head is a nicer window around the exact same screens. Both talk to `TrStudioApi` over HTTP (the desktop head defaults to `http://localhost:5102`; override with the `TrStudioApiBaseUrl` environment variable).

**Distribution consequence (per TrSetup guide §1b):**
- `TrStudioApi` and the `TrStudio` web head are plain .NET → **Option B works**: cross-publish on Windows, copy the folder to the Mac, run — no .NET on the Mac needed.
- `TrStudio.Desktop` is MAUI → the Mac `.app` can **only be built on the Mac** (Option A). The Windows exe cannot be copied over.

## 2. Build & run on Windows (Visual Studio)

1. Open `TrStudio.slnx` in Visual Studio 2022 (17.10 or newer — required to read the modern XML `.slnx` solution format; the `dotnet` CLI and VS Code open it natively) with the .NET 10 SDK + the **.NET MAUI workload**; the private TechieRag feed in `nuget.config` must authenticate — PAT with `read:packages`.
2. Solution → *Configure Startup Projects…* → **Multiple startup projects**: `TrStudioApi` + **either** `TrStudio` (browser) **or** `TrStudio.Desktop` (native window) → Start.
3. For `TrStudio.Desktop`, pick framework `net10.0-windows10.0.19041.0` in the debug-target dropdown. The csproj lists both TFMs unconditionally, so **on Windows always build it with `-f net10.0-windows10.0.19041.0`** — building all frameworks would try (and fail) to build Mac Catalyst without a Mac.
4. CLI equivalents:
   ```powershell
   dotnet run --project src/TrStudioApi                                        # backend on :5102
   dotnet run --project src/TrStudio                                           # web head on :5099
   dotnet build src/TrStudio.Desktop -f net10.0-windows10.0.19041.0 -c Release # desktop exe
   # exe: src\TrStudio.Desktop\bin\Release\net10.0-windows10.0.19041.0\win10-x64\TrStudio.Desktop.exe
   # (unpackaged MAUI app — distribute the whole win10-x64 folder, not the lone exe)
   ```
5. Runtime prerequisites on Windows (same list as the Mac's §4): PostgreSQL + PgVector, ffmpeg, and the isolated Python/ComfyUI env for local generation.

## 3. Run on the Mac — two options

### Option B — copy-to-Mac binaries (no .NET on the Mac) — the recommended first path

The API + web head are plain .NET, so the true "build on Windows, copy, run" flow works. You get the complete app in Safari/Chrome on the Mac; only §4's runtime prerequisites (database, ffmpeg, …) are still needed for actual generation.

**On the Windows machine:**

```powershell
dotnet publish src/TrStudioApi -c Release -r osx-arm64 --self-contained true -o publish\mac\TrStudioApi
dotnet publish src/TrStudio    -c Release -r osx-arm64 --self-contained true -o publish\mac\TrStudio
```

(`-r osx-arm64` targets Apple Silicon; `--self-contained true` bundles the .NET runtime into each folder — the folders are ~100 MB each and **must be copied whole**.)

⚠ **These two commands are the complete Option B list — `TrStudio.Desktop` is deliberately NOT here.** `-r osx-arm64` only cross-compiles plain .NET (`TrStudioApi`, `TrStudio`); it cannot produce the MAUI/Catalyst `.app` from Windows — attempting it just hits the csproj's macOS-only TFM guard (`NETSDK1005`/unknown-TFM). The native desktop head's Mac build is Option A below: one command, on the Mac. There is no `-r osx-arm64` MAUI build, by design — not a missing feature of this guide.

**Copy** both `publish\mac\*` folders to the Mac (Finder `Cmd+K` → `smb://<windows-ip>`, `scp -r`, AirDrop a zip, or USB).

**On the Mac:**

```bash
chmod +x TrStudioApi/TrStudioApi TrStudio/TrStudio      # SMB/zip copies strip the execute bit
xattr -dr com.apple.quarantine TrStudioApi TrStudio     # clear Gatekeeper quarantine (unsigned build)
./TrStudioApi/TrStudioApi &                             # backend first → :5102
./TrStudio/TrStudio &                                   # web head → :5099
open http://localhost:5099
```

Set `ASPNETCORE_URLS=http://localhost:5099` (etc.) before launching if a port needs to be explicit. If Gatekeeper still complains, allow once via System Settings → Privacy & Security.

### Option A — native Mac desktop app (Catalyst, built on the Mac)

> ✅ **UNBLOCKED same day (2026-07-12): TrBlazeUI 1.0.9 fixed TR-011 and the first Catalyst `TrStudio.app` built 0/0 + launched on the owner's Mac.** (The initial real-Mac attempt on 1.0.7 failed `NETSDK1082` — the packages' `Microsoft.AspNetCore.App` FrameworkReference had no maccatalyst runtime pack; see `docs/TrStudio-TrBlazeUI-Feedback.md` TR-011 for the full analysis.) While the MacCatalyst workload band trails the installed Xcode by a minor version, add `-p:ValidateXcodeVersion=false` to the build (the SDK's own opt-out; a `sudo dotnet workload update` that catches the band up removes the need).

**Prerequisites** (TrSetup's Mac roles install all of these): **full Xcode** with licence accepted (Command Line Tools alone are not enough), .NET 10 SDK, `sudo dotnet workload install maui`, git, and the TechieRag feed PAT (`nuget.config` is **gitignored** — it does *not* come with a copy of the source tree; recreate it from `nuget.config.template`. Note the Desktop head itself doesn't reference TechieRag, so it builds without the PAT — the API does need it).

```bash
git clone <TrStudio repo> && cd TrStudio     # or copy the source tree over the LAN (exclude bin/, obj/)
dotnet build src/TrStudio.Desktop -f net10.0-maccatalyst -c Release -p:EnableWindowsTargeting=true -p:ValidateXcodeVersion=false
open src/TrStudio.Desktop/bin/Release/net10.0-maccatalyst/maccatalyst-arm64/TrStudio.app
```

(`-p:EnableWindowsTargeting=true` is required on macOS for **any** command that restores the Desktop project or the solution — its Windows TFM is listed unconditionally and macOS refuses to restore it without the flag, `NETSDK1100`.)

Ad-hoc signing is fine for personal use. The desktop head still needs `TrStudioApi` running somewhere — either the Option B copied binary on the same Mac (default `http://localhost:5102` just works), or point it elsewhere:

```bash
TrStudioApiBaseUrl=http://<windows-ip>:5102 open src/TrStudio.Desktop/bin/Release/net10.0-maccatalyst/maccatalyst-arm64/TrStudio.app
```

**Mix-and-match tip:** the fastest good setup on the Mac is *Option B for the API* (no toolchain needed) + *Option A for the desktop head* (one build once TrSetup has installed the toolchain). Until then, Option B alone gives you everything in the browser.

**Secrets caution (both options):** `appsettings.json` travels inside the publish folders. Keep real keys (RunPod, HeyGen, AppManager) out of the copied files — supply them on the Mac via environment variables (`RunPod__ApiKey=…` style) or a local `appsettings.Production.json` that never leaves the machine.

## 4. Mac runtime prerequisites (what TrStudio needs to actually generate)

TrSetup's "app runner" role will install all of these; manual list until then:

| # | Requirement | Check | Install |
|---|---|---|---|
| 1 | PostgreSQL 16+ running | `psql --version` / port 5432 | `brew install postgresql@16 && brew services start postgresql@16` |
| 2 | **PgVector extension** | `CREATE EXTENSION IF NOT EXISTS vector;` succeeds | `brew install pgvector` |
| 3 | `trstudio` database + user matching the connection string | API starts without DB errors | create DB/user per `TrStudioApi/appsettings.json` (`ConnectionStrings:Default` — override the password via env var rather than editing the copied file) |
| 4 | ffmpeg on PATH | `ffmpeg -version` | `brew install ffmpeg` |
| 5 | Isolated Python + **ComfyUI** | Admin → Hardware & readiness shows green | **TrSetup installs the runtime**; model/workflow provisioning then runs inside the app's **admin area guided setup** (`/admin/*`, role-gated) — that part stays in the app |
| 6 | API keys present (only for the tiers you use) | Admin → Provider config | `RunPod:ApiKey` (High tier), HeyGen key (Premium), AppManager key (identity) — env vars preferred |
| 7 | Disk space for models | — | ~400 GB free is the BRD reference for a full local library; less is fine for light use |

The Mac **is** the natural TrStudio host (capability detection targets Apple Silicon/MPS) — Fast-tier local generation only works on a machine with MPS, i.e. this Mac, not the Windows box.

## 5. First run on the Mac

1. Start `TrStudioApi`, then a UI head (web `:5099` or the Catalyst desktop app) → log in (AppManager superadmin bootstrap). With setup incomplete you land in the **setup-required gate** (BRD-85), which routes admins into guided setup.
2. Complete: hardware readiness → guided setup → Model sources → install at least one model (admin-role screens under `/admin/*`).
3. Back in the creator screens: generate a test image (Fast tier, local MPS) → check it appears in the Gallery with provenance.

## 6. Where TrSetup takes over

Once TrSetup P3 ships, §4 collapses to: *run TrSetup on the Mac → role "App runner" → app "TrStudio" → Fix all*, and the Option A toolchain (§3) is one more Fix click. This guide stays as the manual reference.

---
*Created 2026-07-05; rewritten 2026-07-06 for the unified role-based app (ADR-012): `TrStudio.Desktop` MAUI head added, `TrStudioAdmin` head retired, API moved to :5102. Ports from each head's `launchSettings.json` + `TrStudio.Desktop/MauiProgram.cs`. Clarified 2026-07-10: the plain-.NET-only scope of the `-r osx-arm64` cross-publish made explicit (§ intro + §3 Option B) — the MAUI head is never cross-published; its Mac `.app` is Option A on the Mac. Stale pointer to the (since rewritten) TrSetup guide §1b removed. Updated 2026-07-12 after the source moved to the Mac: Option A marked BLOCKED by TrBlazeUI 1.0.7 TR-011 (`NETSDK1082`, no maccatalyst runtime pack for its `Microsoft.AspNetCore.App` FrameworkReference), `-p:EnableWindowsTargeting=true` added to the Mac commands, and the gitignored-secrets caveat (`nuget.config`, `appsettings.json`) called out. Full Mac dev-run steps: `docs/TrStudio-UsageGuide.md` §How to run — macOS.*
