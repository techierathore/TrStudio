# TrStudio — Developer Guide (Screen-by-Screen Code Map) · Index

> ✅ **Runtime-verified 2026-07-05 (unified role-based app)** — after the 2026-07-05 consolidation (BRD-86 / ADR-012: the two apps became ONE role-gated `TrStudio` app; `TrStudioAdmin` retired), the app was re-exercised as Creator, Operator, and Owner via the `/dev/session` seam on the single app (:5099) and, for the new **MAUI desktop head** (`TrStudio.Desktop`), via a FlaUI native-window smoke on **Windows** (Mac Catalyst builds but is unverified — no Mac in this env). Live evidence: Creator → studios only + `/admin/*` → in-app AccessDenied + admin API 403; Operator → 7 management screens render, API 200; Owner → both surfaces + in-app switch. Screenshots in `docs/screenshots/TrStudio/`. The 9 FE↔API route/visual defects noted below were caught + fixed on 2026-07-04 and remain fixed through the consolidation.
>
> ℹ️ **This is an `--update` realignment**: the per-screen control/data-lineage bodies are unchanged by the consolidation (same endpoints/services/repos); what changed is the **hosting** (management screens moved `TrStudioAdmin/Components/Pages/` → `src/TrStudioUI/Pages/Admin/`), the **role-gated shell**, and **landing**.
>
> ✅ **`*verify all` (2026-07-06) + `*fix-issues` (2026-07-07) — RESOLVED.** The verify pass found 4 screens that didn't reflow at 390px (clipped controls, dead space): Character library, Prompt/script library, Account & spend, Model sources. `*fix-issues` fixed all 4 with page-scoped `@media(≤430px)` single-column collapse + header wrapping + a **systemic shell fix** (the SidebarInset content column + `.table-scroll` now carry `min-width:0`, so wide content scrolls internally instead of inflating the layout). Re-smoked @390px: Character/Library `documentElement.scrollWidth==body==390`; Account/Sources controls all render & fit (`documentElement.scrollWidth==390`, no page scrollbar) with their wide DataTable scrolling internally (a minor DataTable inner-width trait logged as TrBlazeUI **TR-010**). All 4 back to `Verified`. Screenshots `test-results/verify-ui/refix-*.png`.

> **Purpose — the document a HUMAN developer uses to trace any screen, control, or number on the page down to the database, to find/fix a bug or verify AI-generated code.** The BRD says *what* the app does, the Architecture *how the system is shaped*; this guide says exactly which files produce each screen — Razor page → control → client service → API endpoint → engine service → repository → SQL/table — per user role. It documents the **as-built code**; regenerate with `*devguide TrStudio` after code changes.

This is a **split** guide (2 roles, 23 distinct screens → split per the devguide §3 threshold). Open the role file you need:

## Running the solution (start here)

`TrStudio.slnx` is a **7-project** solution in which `TrStudioAdmin` is **RETIRED** (merged into the unified `TrStudio` app on 2026-07-05, ADR-012; removed from the solution) and `TrStudio.Desktop` (a MAUI Blazor Hybrid desktop head) is **NEW**. Running the **web** app means starting **`TrStudioApi` + `TrStudio` together** (API first), with the two Docker dependency containers already up; for the **desktop** app, start **`TrStudioApi` + `TrStudio.Desktop`** instead. The remaining projects are libraries — do **not** set them to start. Full step-by-step (Visual Studio multiple-startup config + CLI) is in **[docs/TrStudio-UsageGuide.md → "Which projects to run" / "How to run"](../TrStudio-UsageGuide.md#which-projects-to-run-this-is-a-multi-project-solution)**. Quick reference:

| Project | Start? | URL / role |
|---------|--------|------------|
| `src/TrStudioApi` | ✅ **Startup (first)** | http://localhost:5102 — Web API, DbUp migrations, Hangfire |
| `src/TrStudio` | ✅ **Startup** | http://localhost:5099 — the **ONE unified role-based** Blazor Web app (creator studios + role-gated management surface) |
| `src/TrStudio.Desktop` | ✅ **Startup — for the DESKTOP app** (instead of `src/TrStudio`) | MAUI Blazor Hybrid **desktop** head (Windows + macOS), hosts the same unified app |
| `src/TrStudioAdmin` | ❌ **RETIRED** | merged into `TrStudio` 2026-07-05 (ADR-012); removed from the solution, not a startup project (old :5098 host gone) |
| `src/TrStudio.Engine`, `src/TrStudio.Models`, `src/TrStudioUI`, `src/TrStudioDb` | ❌ libraries | — (`TrStudioUI` RCL now hosts **all** screens, incl. management under `Pages/Admin/`) |

- **Dependencies (Docker):** `docker start WinPgVectorDb appmanager-api` → `docker ps` (both **Up**) **before** launching. Postgres (`:5432`) and AppManager (`:5101`) both run as containers; a down container is the usual cause of "can't connect"/"Docker" errors at startup.
- **Visual Studio:** right-click solution → *Configure Startup Projects…* → *Multiple* → Start = **`TrStudioApi` + `TrStudio`** (API at the top), everything else = None → F5. Wait for the API console `Now listening on: http://localhost:5102`. (For the desktop app, start **`TrStudioApi` + `TrStudio.Desktop`** instead.)
- The **web** delivery (`src/TrStudio`) is Blazor Web (`net10.0`) — open http://localhost:5099 in the browser (or `:5099/dev/session`). The **desktop** delivery (`src/TrStudio.Desktop`) is a MAUI Blazor Hybrid desktop head (Windows + macOS); MAUI heads are no longer deferred.

## Roles and menu map

| Role | Test user (see UsageGuide) | Authorization (as-built) | App / surface | Detail file |
|------|----------------------------|--------------------------|---------------|-------------|
| **Creator** (standard + Premium) | user 2 `creator@trstudio.test` / user 4 `premium@…` | AppManager JWT + per-handler `ResolveUserId` owner filter; endpoints use the app fallback auth policy (few declarative `.RequireAuthorization()`); client shell shows the Creator surface only (`/admin/*` → AccessDenied) | The ONE `TrStudio` app (`:5099`) — **Creator surface**; screens in the shared RCL `src/TrStudioUI/Pages/` | [TrStudio-DevGuide-Creator.md](./TrStudio-DevGuide-Creator.md) |
| **Operator / Admin** | user 3 `admin@trstudio.test` | client gate: `AppShellNavigation.IsRouteAllowed` → `AccessDenied` for non-management roles; server gate: `SpineModule.AdminOnlyPolicy` = role Owner/Operator (`SpineModule.cs:67-70`), enforced in-handler | The ONE `TrStudio` app (`:5099`) — **Management surface (role-gated)**; screens now in the shared RCL `src/TrStudioUI/Pages/Admin/` | [TrStudio-DevGuide-Operator.md](./TrStudio-DevGuide-Operator.md) |
| **Owner / Superadmin** | user 1 `admin@appmanager.local` | union of both (Creator + Operator) | The ONE `TrStudio` app — lands on the **Creator surface**, switches to the **Management surface** via the in-app sidebar-footer control | Creator + Operator files above |

**Landing-truth (read from code, not inferred — re-read 2026-07-05 after the consolidation):**
- **Creator** lands on `/avatar` after login (`Login.razor:153-166` → `ResolveLanding`, + setup-gate `IdentityEndpoints.cs:313-322`).
- **Operator** lands on `/admin/ops` — **now code-backed** in the unified app (the old two-app "not code-backed" note is superseded): `AppShellNavigation.ResolveDefaultSurface` selects the Management surface for an Operator (`AppShellNavigation.cs:98-106`), `TrStudioNavSets.ManagementLandingPath = "/admin/ops"` (`TrStudioNavSets.cs:14`), and the post-login redirect routes Operator→`/admin/ops` else `/avatar` (`Login.razor:165`; dev seam identical `DevSession.razor:117-118`). Confirmed live (`.smoke/results.json` Operator `landUrl …/admin/ops`).
- **Owner** lands on `/avatar` (Creator surface) and switches to Management in-app via the sidebar footer (`AppSidebar.razor:85-89` → `AppShellNavigation.cs:63-66`, target `/admin/ops`). The dev-session seam resolves the authoritative role via `GET /api/v1/auth/me` (`DevSession.razor`).

## Architecture cheat-sheet

| Layer | Project / folder | What lives here |
|-------|------------------|-----------------|
| UI (Blazor) — shared RCL (ALL screens) | `src/TrStudioUI/` (RCL) | `Pages/` (creator screens) + `Pages/Admin/` (the 7 management screens, moved here 2026-07-05), `Navigation/AppShellNavigation.cs` (role-based surface selection), `Layout/AppSidebar.razor`, `MainLayout.razor`, `Layout/AccessDenied.razor`, `ThemeToggle.razor`, plus the `IAuthSession`/`AuthSessionState` contracts (`Auth/`) — hosted by the `src/TrStudio` web app AND the `src/TrStudio.Desktop` MAUI head |
| Hosts | `src/TrStudio` (Web) · `src/TrStudio.Desktop` (MAUI Win/Mac) | Web host: role-based DI + Server auth impls (`src/TrStudio/Auth/`, ProtectedSessionStorage). Desktop host: `MauiAuthSession` (SecureStorage) + BlazorWebView over the same RCL |
| HTTP API | `src/TrStudioApi/Endpoints/` (+ `Hubs/` SignalR) | Minimal-API endpoint groups (`/api/v1/*`); the Razor pages call these via typed HttpClient services |
| App / domain services | `src/TrStudio.Engine/Domain/<Area>/` | Business logic per area (Image, Avatar, Voice, Video, Characters, Jobs, Compare, Registry, Safety, Cost, RunPod, Comfy, Setup, …) |
| Providers / workers | `src/TrStudio.Engine/Providers/`, `Workers/` | `IGenerationProvider` implementations; Hangfire workers |
| Data access | `src/TrStudio.Engine/Infrastructure/Data/` (+ per-area) | **Dapper repositories** — parameterized SQL literals (PascalCase identifiers quoted). **No EF Core, no stored procs.** |
| Database | `src/TrStudioDb/Scripts/*.sql` | DbUp migrations (tables + dev seeds); run automatically on API startup |

- **How a screen gets its data:** Razor page → injected typed **HttpClient service** → **`/api/v1/*` endpoint** (`src/TrStudioApi/Endpoints/`) → **engine service** (`Engine/Domain/<Area>/`) → **Dapper repository** → **SQL against a table** (defined in `src/TrStudioDb/Scripts/`). The "Stored proc / Query" lineage column names the repository method + its SQL/table, since there are no procs.
- **Auth model:** every user-scoped handler resolves the AppManager `userId` and applies the owner filter server-side (BRD-81); admin endpoints require the Owner/Operator role policy.

## Screen inventory

- **Creator (16 screens):** Login, Register, Forgot-password, Setup-required, Home, Avatar studio (flagship), Character library, Image studio, Voice studio, Video studio, **Prompt studio** (`/prompt-studio`, Phase 10 — added + runtime-verified 2026-07-07), Gallery, Projects, Comparison, Library, Account — see [Creator file](./TrStudio-DevGuide-Creator.md).
- **Operator / Management surface (7 screens, role-gated within the one app):** Hardware & Guided setup (dual route), Model sources, Model library, Registry & providers, RunPod & Ops, ComfyUI import & debug (dual route), Content policy / NSFW — now in `src/TrStudioUI/Pages/Admin/` — see [Operator file](./TrStudio-DevGuide-Operator.md).

## Defects surfaced while mapping (logged to `docs/TrStudio-Checklist.md`)

The DevGuide's static map + runtime OBSERVE caught a cluster of **FE↔API wiring defects** — client calls a route the API doesn't expose, or a save handler that's toast-only, swallowed by try/catch so the screen still demos on seed data (which is why they passed visual verification). **✅ ALL 9 FIXED + RE-VERIFIED LIVE 2026-07-04** (`*fix-issues`) — each row is back to `Verified`; the table below is the record of what was caught and closed:

| # | REQ | Defect (file:line) |
|---|-----|--------------------|
| 1 | UI-071 | `SaveModelAsync`/`SaveKeysAsync` toast-only — zero persistence (`AdminRegistry.razor:346-362`) |
| 2 | UI-072 | ComfyUI FE routes `/status,/run,/install` ≠ BE `/debug,/dependencies,/smoke,/go-live` (`AdminComfy.razor:296-298`) |
| 3 | UI-070 | Guided-setup stepper `RunStepAsync` toast-only — doesn't call `POST /admin/setup/steps/{key}/run` |
| 4 | UI-010 | Avatar voice-preview → `/api/v1/voice` root (misses `/synthesize`); script-assist → non-existent `/rag/script-assist` |
| 5 | UI-040 | Video timeline Edit → `/api/v1/avatar/edit` instead of `/api/v1/video/edit` (`VideoStudio.razor:535`) |
| 6 | UI-030 | Voice `VoiceApiRoute="/api/v1/voice"` → `/presets` & root-POST miss `/preset-voices` & `/synthesize` |
| 7 | UI-053 | Library preset-type `style`/`voice` ≠ API `style-preset`/`voice-preset` → 400 (remark) |
| 8 | UI-011 | Character-bible Sheet renders static copy — never calls `GET /characters/{id}/bible` (remark) |
| 9 | UI-073 | **Runtime (OBSERVE):** `/admin/ops` doesn't reflow at 390px — 823px horizontal overflow, text clipped (`⚠ visual:`) |

**9 defects caught (8 static FE↔API wiring + 1 runtime visual), all FIXED + re-verified live 2026-07-04** — each fixed call now lands on the API request log (200/201; registry edit persists on reload; comfy `/status` probe removed; admin-ops `scrollWidth==390`). Only residual: `/video/edit` returns a clean 404 until a real source clip exists (placeholder-timeline limitation — the route is now correct). Core flows (job spine, tier routing, safety floors, per-user isolation, image ops, provenance, consent gate) were never affected. All nine `REQ-UI-*` rows are `Verified` in `docs/TrStudio-Checklist.md`.

## How to fix a bug with this guide

1. Reproduce; note the **screen** and **control**.
2. Open the role file, find the screen, find the control in its **Data lineage** table.
3. Walk the lineage top-down: Razor → client service → API endpoint → engine service → repository → SQL. The bug is in one of those hops.
4. If a value is *calculated*, the row names the calculator method.
5. After fixing, re-run the screen's walkthrough in `docs/TrStudio-UsageGuide.md`, then re-run `*devguide TrStudio --update`.

---
_Generated by `*devguide TrStudio` · reflects code as built · realigned 2026-07-05 for the one-app role-based consolidation (BRD-86 / ADR-012) + MAUI desktop head. Regenerate after code changes._
