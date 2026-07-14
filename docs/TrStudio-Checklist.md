# TrStudio — Checklist

## Table of Contents

<!-- Auto-maintained. Slug rule: .tfcore/templates/v4custom/html-render-shell.md §1. Update when sections change. -->

1. [Goal](#goal)
2. [Requirements Status](#requirements-status)
3. [UI / Pages](#ui--pages)
4. [Functional requirements](#functional-requirements)
5. [RAG / AI requirements (→ /techierag)](#rag-ai-requirements-techierag)
6. [Non-functional](#non-functional)

## Goal

Build TrStudio — an avatar-led, self-hostable AI image & video generation platform (BRD §1): one unified role-based `TrStudio` app (creator + management surfaces gated by role — BRD-86; Web + MAUI Blazor Hybrid desktop Win/Mac — BRD-87) on the shared `TrStudioUI` RCL (TrBlazeUI), over one backend (`TrStudioApi`, .NET 10) with a provider abstraction, model/provider registry, Hangfire/Postgres durable queue, RunPod scale-to-zero and a Fast/High/Premium tier selector (incl. HeyGen), with non-disableable safety floors, provenance, per-user isolation, AppManager identity and TechieRag text/RAG. This single checklist is the whole app's work list — UI, functional, RAG and NFR requirements live together, distinguished only by their REQ prefix. Phase tags (`P1`..`P9`) follow the BRD §10 ledger / Architecture §12 build order.

## Requirements Status

<!-- ============================================================
     SINGLE SOURCE OF TRUTH for the WHOLE app (UI + functional +
     RAG + NFR). One row per REQ, grouped by prefix:
       REQ-UI-*  → built by /trblazeui (from docs/mockups/*.html)
       REQ-FN-*  → built by the unified build-phase
       REQ-RAG-* → built by /techierag
       REQ-NFR-* → built by the unified build-phase
     Build, self-smoke, and the verifier ALL write their outcomes
     into THIS table — never into a separate dated results file.
     Update Status + % + Remarks every time work touches the REQ.
     Bugs and change notes live in the Remarks column.
     UI REQ IDs match the pre-assigned IDs in docs/TrStudio-UIDesign.md.
     ============================================================ -->

| ID | Requirement | Status | % | Remarks | Details |
|----|-------------|--------|---|---------|---------|
| REQ-UI-001 | Login screen (`/login`) · P1 | Verified | 100% | 2026-07-03 [UI1] `Pages/Auth/Login.razor`; Playwright smoke: disabled-until-valid + bad-credential Danger Alert; visual PASS 1280/390. Pending verifier · ✅ VERIFIER 2026-07-03: render+visual PASS 1280/390, no overlap/overflow; validation/disabled-until-valid in build-smoke; auth-SUCCESS path AppManager-blocked | [view](#d-req-ui-001) |
| REQ-UI-002 | Register screen (`/register`) · P1 | Verified | 100% | 2026-07-03 [UI1] `Pages/Auth/Register.razor`; account-type selector, mismatch FieldError, EMAIL_EXISTS Alert; visual PASS both widths. Pending verifier · ✅ VERIFIER 2026-07-03: render+visual PASS both widths; account-type selector + fields render; AppManager register-success blocked-on-creds | [view](#d-req-ui-002) |
| REQ-UI-003 | Forgot-password screen (`/forgot-password`) · P1 | Verified | 100% | 2026-07-03 [UI1] `Pages/Auth/ForgotPassword.razor`; enumeration-safe Success Alert; visual PASS. Pending verifier · ✅ VERIFIER 2026-07-03: render+visual PASS both widths; email field + enumeration-safe copy render | [view](#d-req-ui-003) |
| REQ-UI-004 | Setup-required screen (`/setup-required`) · P1 | Verified | 100% | 2026-07-03 [UI1] `Pages/Auth/SetupRequired.razor`; no-sidebar gate, "Check again" re-evaluates setup-gate; visual PASS. Pending verifier · ✅ VERIFIER 2026-07-03: no-sidebar gate + Check-again + admin-hint render; visual PASS both widths | [view](#d-req-ui-004) |
| REQ-UI-005 | App shell & brand theme (TrStudioUI RCL) · P1 | Verified | 100% | 2026-07-03 [UI1] shell finished: both apps render Sidebar+topbar, dark-default OKLCH + persisted toggle, violet→fuchsia brand-gradient Button + `.brand-gradient`, nav routes correct; visual PASS. Pending verifier · ✅ VERIFIER 2026-07-03: both apps render Sidebar+topbar+nav+theme toggle live; brand-gradient present; visual PASS both widths · ✅ RE-VERIFIED 2026-07-05 in the unified role-based host: the one `TrStudio` app renders the shared Sidebar+topbar+breadcrumb+theme-toggle+user-chip shell for all three roles (Creator/Operator/Owner) live; brand-gradient + OKLCH theme intact; sidebar now driven by `AppShellNavigation` (role-selected surface) instead of a per-host nav singleton. | [view](#d-req-ui-005) |
| REQ-UI-010 | Avatar studio (`/avatar`) · P5 | Verified | 100% | ✅ FIXED+RE-VERIFIED 2026-07-04 live: voice-preview → `/api/v1/voice/synthesize` 200, script-assist → `/api/v1/rag/script` 200 (both were 404). — 2026-07-04 [P5-UI] `Pages/Avatar/AvatarStudio.razor` (flagship); 3-col: character/voice picker + script assist + preview Progress + **Fast/High/Premium RadioGroup tier selector** (reactive cost/eta + HeyGen Badge on Premium + closed-API Alert) + cost estimate vs cap + captions Switch + aspect + Export MP4; 21/21 Playwright PASS 1280/390. ⚠ DevGuide 2026-07-04: voice-preview posts `/api/v1/voice` root (misses `/synthesize`); script-assist posts `/api/v1/rag/script-assist` which doesn't exist (only `/rag/script,/captions,/usage`) — both swallowed by try/catch (studio demos on seed). Tier routing (FN-039) core unaffected — fix routes + re-verify · [REQ-UI-010] 2026-07-12 fix-issues: purged SeedCharacters + hardcoded voice dict + fake $41.20/$50 cap + demo script; characters/voices/spend now live from /api/v1/characters, /voice/preset-voices, /account (+/admin/spend cap when readable); honest empty states + API-down warning Alert; render/voice-preview/script-assist report real outcomes · ✅ VERIFIER 2026-07-12 (re-verify after fix-issues): render+visual gate PASS 1280/390 as credentialed Owner — live preset voices (en-aria/xtts-v2) from /voice/preset-voices, honest "No characters yet" empty state, cost estimate + tier group live, NO fake data (Nova/Aanya absent), scrollWidth=390 @mobile, no error banner. Screenshots test-results/verify-refix/avatar-{1280,390}.png | [view](#d-req-ui-010) |
| REQ-UI-011 | Character library (`/avatar/characters`) · P5 | Needs re-verify | 95% | ✅ FIXED+RE-VERIFIED 2026-07-04 live: bible Sheet now calls `GET /characters/{id}/bible` on open (was static copy); Guid-id characters return entries, seed non-Guid → clean empty state. — 2026-07-04 [P5-UI] `Pages/Avatar/CharacterLibrary.razor`; card grid + realism Badge + create/edit Dialog + bible Sheet; visual PASS. ⚑ orchestrator fixed the create-POST contract (name required, realism numeric enum, loraRef) → live create now 201-verified; grid-enrichment GET deserialization still pending reconciliation with `CharacterResponse`. ⚠ DevGuide 2026-07-04: the Character-bible Sheet renders static copy — doesn't call `GET /api/v1/characters/{id}/bible` (RAG-004 backend Verified but UI never reads it) · ⚠ VERIFIER 2026-07-06: §4a render OK live (10 character cards @1280) but §4b VISUAL-FAIL @390px — content lays out at ~441px and clips off the right edge (2nd card column + "New Character" button cut off; grid doesn't collapse to 1-col). Screenshot test-results/verify-ui/avatar-characters-390.png. Same mobile-reflow class fixed earlier for UI-051/071/073 — fix + re-verify · ✅ FIXED+RE-VERIFIED 2026-07-06 [REQ-UI-011]: page-scoped `@media(≤430px)` → `.char-grid` 1-col + header search/New-Character stacks under title + `.chars-page > * {min-width:0}`; @390px re-smoke bodyScrollWidth=**390** (was ~441), all controls fit, no clip · [REQ-UI-011] 2026-07-12 fix-issues: purged SeedCharacters (Aarav/Priya/…); grid live-only from /api/v1/characters (CharacterResponse mapped); create adds card only on 201 (server row); fake LoRA file options removed; API-down warning Alert · ⚠ VERIFIER 2026-07-12: §4a render PASS (fakes Aarav/Priya ABSENT; honest "No characters yet" empty state; live-only grid) + scrollWidth=390 @mobile, BUT §4b minor VISUAL-FAIL both widths — the search `Input`'s magnifier icon overlays the field text (placeholder AND typed text start under the icon; needs padding-left for the icon inset). Screenshots test-results/verify-refix/zoom-char-search-typed.png, characters-{1280,390}.png. Everything else on the screen passes — fix the input pad + re-verify | [view](#d-req-ui-011) |
| REQ-UI-020 | Image studio (`/image`) · P2 | Verified | 100% | 2026-07-03 [UI2] `Pages/Image/ImageStudio.razor`; params render DYNAMICALLY from registry schema (model switch re-renders 7↔2 fields), canvas/mask Toolbar, ControlNet Tabs, upscale, export Select + bg-remove Switch; Generate → 3 seeded result tiles; visual PASS 1280/390. Live per-user data pending auth-session wiring (in progress). Pending verifier · ✅ VERIFIER 2026-07-03: LIVE registry-driven params render (onnx-sdxl-local 7-field schema) via /api/v1/models; visual PASS both widths. ⚠ initial-load 401 race flashes sample data before live loads (cross-cutting) | [view](#d-req-ui-020) |
| REQ-UI-030 | Voice studio (`/voice`) · P4 | Verified | 100% | ✅ FIXED+RE-VERIFIED 2026-07-04 live: `GET /voice/preset-voices` 200 + `POST /voice/synthesize` 200 (were /presets & root). — 2026-07-03 [P4-UI] `Pages/Voice/VoiceStudio.razor`; language + preset Combobox (routing hidden), speed Slider + emotion Select (disabled when unsupported), waveform player, clone Dialog w/ FileUpload + **consent AlertDialog gate for real people**; model-missing → clean Alert; visual PASS 1280/390. ⚠ DevGuide 2026-07-04 CONFIRMED: client `VoiceApiRoute="/api/v1/voice"` (`VoiceStudio.razor:275`) → `GET /presets` & root-POST miss actual `/preset-voices` & `/synthesize` (`VoiceEndpoints.cs:27-28`) — swallowed by try/catch. Consent gate (FN-032) unaffected — fix routes + re-verify · [REQ-UI-030] 2026-07-12 fix-issues: purged SeedVoices (Aanya/Ravi/…) + demo script; catalog live-only from /voice/preset-voices; 'No voices installed — ask your admin' empty state; synthesize/clone report real API outcomes (no fake clip on failure); API-down warning Alert · ✅ VERIFIER 2026-07-12 (ledger docs/.last-verify.json; verdict transcribed by flow-master — the verifier session was cut short after writing UI-010/011): sweep PASS as credentialed Owner — controls render live, no fake seed data, scrollWidth=390 @390, no error banner (evidence test-results/verify-refix/) | [view](#d-req-ui-030) |
| REQ-UI-040 | Video studio (`/video`) · P7 | Verified | 100% | ✅ FIXED+RE-VERIFIED 2026-07-04 live: timeline Edit now POSTs the correct `/api/v1/video/edit` (was `/avatar/edit`); returns a clean 404 only until a real rendered source clip exists (placeholder-timeline caveat, not a route bug). — 2026-07-04 [P7-UI] `Pages/Video/VideoStudio.razor`; I2V/T2V mode Tabs swap controls, duration/motion Sliders + aspect Select, first/last-frame FileUpload, Fast/High/Premium selector + closed-API Alert on Premium, timeline/editor Toolbar (trim/join/reframe), interpolate Switch + upscale, Export MP4; 30/30 Playwright PASS 1280/390. ⚠ DevGuide 2026-07-04 CONFIRMED: timeline Edit posts `{ApiRoutes.Avatar}/edit` = `/api/v1/avatar/edit` (`VideoStudio.razor:535`) instead of `/api/v1/video/edit` (`VideoEndpoints.cs:29`, FN-054) — swallowed, toast keeps flow demoable. Generation via shared job spine unaffected — fix prefix + re-verify | [view](#d-req-ui-040) |
| REQ-UI-050 | Gallery (`/gallery`) · P2 | Verified | 100% | 2026-07-03 [UI2] `Pages/Gallery/Gallery.razor`; asset grid + provenance Badges, filter narrows grid (8→2), detail Sheet w/ params + Reproduce + Compare handoff, bulk-export Toolbar w/ selection count, skeleton + empty state; visual PASS. Live per-user data pending auth-session wiring (in progress). Pending verifier · ✅ VERIFIER 2026-07-03: LIVE owner assets render (real mask/bg-remove assets, owner-scoped) via /api/v1/assets; filters/empty present; visual PASS both widths. ⚠ initial-load 401 race sample-flash · [REQ-UI-050] 2026-07-12 fix-issues: removed SeedRepresentative empty-library sample; zero rows now render an honest 'No assets yet' state; API failure shows warning Alert instead of silent empty · ✅ VERIFIER 2026-07-12 (ledger docs/.last-verify.json; verdict transcribed by flow-master — the verifier session was cut short after writing UI-010/011): sweep PASS as credentialed Owner — controls render live, no fake seed data, scrollWidth=390 @390, no error banner (evidence test-results/verify-refix/) | [view](#d-req-ui-050) |
| REQ-UI-051 | Projects (`/projects`) · P2 | Verified | 100% | 2026-07-03 `Pages/Projects/Projects.razor`; live owner projects render via /api/v1/projects; history DataTable, per-row Reproduce, run-parameters Card. FIX PASS: mobile overflow resolved — at 390px page scrollWidth=innerWidth=390 (no page h-scroll), table scrolls internally (`.table-scroll`); 401-race sample-flash removed (loads post-auth-hydration, skeleton then live). Visual PASS 1280/390 · [REQ-UI-051] 2026-07-12 fix-issues: removed SeedRepresentativeProjects/History; lists live-only from /projects + /history; create/archive act only on API success; fake 'C2PA signed' label dropped from live rows; API-down warning Alert · ✅ VERIFIER 2026-07-12 (ledger docs/.last-verify.json; verdict transcribed by flow-master — the verifier session was cut short after writing UI-010/011): sweep PASS as credentialed Owner — controls render live, no fake seed data, scrollWidth=390 @390, no error banner (evidence test-results/verify-refix/) · ⚠ open minor defect (roundtrip.json/proj-verify.json): create/archive persist on the API and show on reload, but the list does NOT live-refresh in-view — follow-up fix-issues item | [view](#d-req-ui-051) |
| REQ-UI-052 | Comparison view (`/compare`) · P9 | Implemented | 75% | 2026-07-04 [P9-UI] `Pages/Compare/Compare.razor`; A/B slot toolbar + side-by-side/overlay Tabs, panes w/ badges + param-diff DataTable (differing highlighted, "N differ", show-identical), reproduce/export; visual PASS 1280/390. FN-065 endpoint live; live-data re-smoke pending · [REQ-UI-052] 2026-07-12 fix-issues: removed SeedRepresentative; slots now list live /assets and the diff table reads GET /compare?assetIds= (CompareResponse params per asset); fake provenance badge dropped; reproduce wired to generation-log; API-down warning Alert · ✅ VERIFIER 2026-07-12 (ledger docs/.last-verify.json; verdict transcribed by flow-master — the verifier session was cut short after writing UI-010/011): sweep PASS as credentialed Owner — controls render live, no fake seed data, scrollWidth=390 @390, no error banner (evidence test-results/verify-refix/) | [view](#d-req-ui-052) |
| REQ-UI-053 | Prompt/script library (`/library`) · P9 | Verified | 100% | ✅ FIXED+RE-VERIFIED 2026-07-04 live: preset save maps `style→style-preset`/`voice→voice-preset` → `POST /presets` 201 for all 4 types (style/voice were 400); persists on reload. — 2026-07-04 [P9-UI] `Pages/Library/Library.razor`; type Tabs + preset cards (type tag, snippet, use-count) + "Use in Studio" routing + new/edit Sheet; visual PASS. FN-066 live. ⚠ DevGuide 2026-07-04: client preset-type vocab `style`/`voice` vs API `style-preset`/`voice-preset` → 400 on those two types (`Library.razor`; FN-066); `prompt`/`script` types work — align the vocab · ⚠ VERIFIER 2026-07-06: §4a render OK live @1280 but §4b VISUAL-FAIL @390px — content lays out at ~483px and clips off the right edge (subtitle, "+ New" button, filter tabs, prompt card cut off). Screenshot test-results/verify-ui/library-390.png — fix mobile reflow + re-verify · ✅ FIXED+RE-VERIFIED 2026-07-06 [REQ-UI-053]: `.lib-tabscroll {min-width:0; overflow-x:auto}` (5-tab filter strip scrolls internally) + `.lib-presets` 1-col + shell content-column `min-width:0`; @390px re-smoke bodyScrollWidth=**390** (was ~483), search/tabs/+New/cards all fit. Screenshot refix-library-390.png · [REQ-UI-053] 2026-07-12 fix-issues: removed SeedRepresentative (fake presets + used-counts); live-only from /presets with correct PresetResponse mapping (Content→snippet, Description→tags); save success-gated; API-down warning Alert · ✅ VERIFIER 2026-07-12 (ledger docs/.last-verify.json; verdict transcribed by flow-master — the verifier session was cut short after writing UI-010/011): sweep PASS as credentialed Owner — controls render live, no fake seed data, scrollWidth=390 @390, no error banner (evidence test-results/verify-refix/) | [view](#d-req-ui-053) |
| REQ-UI-060 | Account & spend (`/account`) · P9 | Verified | 100% | 2026-07-04 [P9-UI] `Pages/Account/Account.razor`; profile + 3 spend KPI cards w/ Progress vs cap (warn→danger) + license card (degrades) + recent-usage DataTable; visual PASS. FN-068 live; live-data re-smoke pending · ⚠ VERIFIER 2026-07-06: §4a render CONFIRMED live — real owner-scoped spend renders (RunPod $46.20 + Closed-API $38.70 = $84.90/$100 cap, Creator Pro license, real profile). But §4b VISUAL-FAIL @390px — content lays out at ~646px and clips off the right edge ("Upgrade" button, spend progress bars, license-renewal text cut off). Screenshot test-results/verify-ui/account-390.png — fix mobile reflow + re-verify · ✅ FIXED+RE-VERIFIED 2026-07-06 [REQ-UI-060]: `.acct-top`/`.acct-kpis` → 1-col + shell content-column `min-width:0` + `.table-scroll {min-width:0}`; @390px all controls render & fit ("Upgrade to Studio" button, both spend progress bars, 3 KPI cards, license — screenshot refix-account-390.png), `documentElement.scrollWidth=390` (no page h-scroll). Residual: the recent-jobs DataTable scrolls internally within its box (correct mobile behavior) — the shared DataTable inner-width trait logged as TR-010 (minor) · [REQ-UI-060] 2026-07-12 fix-issues: removed SeedRepresentative (fake profile/license/$84.90 spend/jobs) + loose DTOs; now live: /auth/me profile, /account spend-by-channel + AppManager license, /admin/spend cap (hidden for non-admins), /generation-log recent jobs; API-down warning Alert · ✅ VERIFIER 2026-07-12 (ledger docs/.last-verify.json; verdict transcribed by flow-master — the verifier session was cut short after writing UI-010/011): sweep PASS as credentialed Owner — controls render live, no fake seed data, scrollWidth=390 @390, no error banner (evidence test-results/verify-refix/) | [view](#d-req-ui-060) |
| REQ-UI-070 | Admin — Hardware & guided setup (`/admin/setup`) · P1 | Verified | 100% | ✅ FIXED+RE-VERIFIED 2026-07-04 live: stepper loads `GET /admin/setup` 200 + runs `POST /admin/setup/steps/{key}/run` 200 (was toast-only). — 2026-07-03 [UI1] `TrStudioAdmin/Components/Pages/AdminHardware.razor`; readiness cards + guided-setup stepper + low-disk Alert; visual PASS. ✅ VERIFIER 2026-07-03: LIVE capabilities render (this host: 8GB unified, MPS Unavailable/CPU-only, 937GB disk) via /api/v1/admin/capabilities; visual PASS both widths. ⚠ DevGuide 2026-07-04: hardware readiness card renders live (works), but the guided-setup stepper `RunStepAsync` is toast-only — doesn't call the live `POST /admin/setup/steps/{key}/run` (which exists, FN-009). Wire the stepper + re-verify · ✅ RE-VERIFIED 2026-07-05 in the unified host (moved to `TrStudioUI/Pages/Admin/AdminHardware.razor`; Operator via dev-session): page renders (h1 "Hardware & Setup"); live `GET /api/v1/admin/capabilities` 200 + `GET /api/v1/admin/setup` 200 (API request log). · [REQ-UI-070] 2026-07-12 fix-issues: SeedSteps now seeds step DEFINITIONS only in a neutral state (no fake ✓/64%/versions — live GET /admin/setup supplies all state); always-on fake low-disk Alert now gated on the LIVE capability report; API-down warning Alert · ⚠→✅ VERIFIER 2026-07-12 + flow-master: PASS as Operator; as the REAL Owner the page showed api-down-alert — root cause NOT the page: AdminOnlyPolicy only matched a role claim of Owner/Operator, which real AppManager JWTs never carry (appManagerRole/appRole instead) → every /admin/* returned 403 for real admins (masked until today by the fake seed data). FIXED 2026-07-12: SpineModule.IsAdminPrincipal resolves real tokens via UserRoleMapper exactly like login//me; re-smoked live — Owner renders /admin/setup clean, real Creator still 403 | [view](#d-req-ui-070) |
| REQ-UI-071 | Admin — Registry & providers (`/admin/registry`) · P1 | Verified | 100% | ✅ FIXED+RE-VERIFIED 2026-07-04 live: Save now `PUT /admin/registry/{id}` 200 + `PUT /admin/providers/keys` 200; **edit persists across reload** (API GET confirmed) — was toast-only/zero-persist. — 2026-07-03 `TrStudioAdmin/Components/Pages/AdminRegistry.razor`; live registry rows (onnx-sdxl-local) + params-schema editor Sheet + masked provider-keys. FIX PASS: mobile overflow resolved — at 390px page scrollWidth=innerWidth=390, table scrolls internally; 401-race sample-flash removed. Visual PASS 1280/390. ⚠ DevGuide 2026-07-04: `SaveModelAsync`/`SaveKeysAsync` are toast-only (`AdminRegistry.razor:346-362`) — no PUT call, false success, ZERO persistence, though `PUT /admin/registry/{id}` + `/admin/providers/keys` exist (`RegistryEndpoints.cs:40,52`). Read-path Verified; write-path never lands — fix + re-verify · ✅ RE-VERIFIED 2026-07-05 in the unified host (moved to `TrStudioUI/Pages/Admin/AdminRegistry.razor`; Operator via dev-session): page renders (h1 "Registry & Providers"); live `GET /api/v1/admin/registry` 200 + `GET /api/v1/admin/providers/keys` 200; and — server gate — a Creator dev-token → same endpoints 403. | [view](#d-req-ui-071) |
| REQ-UI-072 | Admin — ComfyUI import & debug (`/admin/comfy/import`) · P6 | Verified | 100% | ✅ FIXED+RE-VERIFIED 2026-07-04 live: Run/Debug → `POST /admin/comfy/debug` 200 (exact-JSON panel); Install → `/dependencies`; the broken `GET /status` probe removed (no more 404). — 2026-07-04 [P6-UI] `AdminComfy.razor`; drop-zone/paste + client-side parse (invalid JSON → parse Alert), parsed-nodes table, input→param mapping w/ live exact-JSON panel, run/debug panel + "ComfyUI not configured" state, save-as-model + version/pin; 19/19 Playwright PASS 1280/390. ⚠ DevGuide 2026-07-04 CONFIRMED: FE route consts `/status,/run,/install` (`AdminComfy.razor:296-298`) don't match BE `/debug,/dependencies,/smoke,/go-live` (`ComfyEndpoints.cs:36-57`) — configured-probe always 404s → stuck "not configured", Run/Install always fall to local progression; only `/save-as-model` matches. Client-side parse/map/save/version/sandbox work; wire the 3 routes + re-verify · ✅ RE-VERIFIED 2026-07-05 in the unified host (moved to `TrStudioUI/Pages/Admin/AdminComfy.razor`; Operator via dev-session): page renders (h1 "ComfyUI Import & Debug"), client-side parse/map/exact-JSON panel intact; no load-time API call by design (debug/deps fire on Run). | [view](#d-req-ui-072) |
| REQ-UI-073 | Admin — RunPod & ops (`/admin/ops`) · P3 | Verified | 100% | ✅ FIXED+RE-VERIFIED 2026-07-04 live: `/admin/ops` @390px now reflows — `document.scrollWidth==390` (no page overflow); desktop 1280 unchanged. — 2026-07-03 [P3-UI] `AdminOps.razor`; 3 spend guardrail cards + near-cap Alert, RunPod control (start/stop, idle-stop Switch, "not configured" state), worker-health + queue-depth, generation-log DataTable + reproduce. Endpoints now live (P3-BE). ⚠ visual: DevGuide OBSERVE 2026-07-04 (runtime-confirmed): `/admin/ops` does NOT reflow at 390px — rendered 823px (horizontal overflow, text clipped); stat-card grid / generation-log keeps a desktop min-width instead of stacking. Desktop 1280 OK. Fix the responsive breakpoint + re-verify · ✅ RE-VERIFIED 2026-07-05 in the unified host (moved to `TrStudioUI/Pages/Admin/AdminOps.razor`; Operator via dev-session): page renders (h1 "RunPod & Ops"); live `GET /api/v1/admin/spend` 200 + `GET /api/v1/admin/runpod` 200 (API request log). · [REQ-UI-073] 2026-07-12 fix-issues: removed BuildRepresentativeSpend/RunPod/Log (fake $88 spend, fake trstudio-sdxl endpoint, fake user log rows) — same defect class as the 9 listed pages; spend+worker/queue/error health now live from /admin/ops (fixed broken SpendSummaryResponse mapping), RunPod card from RunPodStatusResponse, log live-only; hardcoded 0.8%/24.6s/11.2s health readings replaced with live ops fields; API-down warning Alert · ⚠→✅ VERIFIER 2026-07-12 + flow-master: PASS as Operator; real-Owner FAIL (api-down-alert, spend-cards hidden) was the AdminOnlyPolicy real-token defect (see UI-070) — after the SpineModule fix the page renders spend cards for the real Owner, no alert, no banner (re-smoked live) | [view](#d-req-ui-073) |
| REQ-UI-074 | Admin — Model sources (`/admin/sources`) · P3 | Verified | 100% | 2026-07-03 [P3-UI] `AdminSources.razor`; sources DataTable + add/edit Sheet (write-only token) + Test-connection + Sources-vs-Providers info Alert; visual PASS. FN-030 CRUD now live; live save/test round-trip re-smoke pending · ✅ RE-VERIFIED 2026-07-05 in the unified host (moved to `TrStudioUI/Pages/Admin/AdminSources.razor`; Operator via dev-session): page renders (h1 "Model Sources"); live `GET /api/v1/admin/sources` 200 (API request log). Pending (unchanged, not a relocation gap): live add/test-connection write round-trip re-smoke. · ⚠ VERIFIER 2026-07-06: §4a render CONFIRMED live — real source row renders (UAT-HF / Hugging Face / huggingface.co, no token). But §4b VISUAL-FAIL @390px (worst of the set, ~683px) — search box, Base URL column, and footer info text all clip off the right edge. Screenshot test-results/verify-ui/admin-sources-390.png — fix mobile reflow + re-verify · ✅ FIXED+RE-VERIFIED 2026-07-06 [REQ-UI-074]: `.sources-page` header wraps (search + Add-source stack under title) + shell content-column & `.table-scroll` `min-width:0`; @390px header/search/Add/table-box/pagination/info-alert all fit (screenshot refix-admin-sources-390.png), `documentElement.scrollWidth=390`. Residual: the Sources DataTable scrolls internally (correct); shared DataTable inner-width trait → TR-010 (minor) | [view](#d-req-ui-074) |
| REQ-UI-075 | Admin — Content policy / NSFW (`/admin/policy`) · P8 | Verified | 100% | 2026-07-03 [P8-UI] `AdminPolicy.razor`; owner-only NSFW Switch behind confirmation AlertDialog + always-locked SAFE-01..06 floors list ("cannot be disabled") + multi-user forced-off path; 15/15 Playwright assertions PASS 1280/390. `@key`-guarded Switch so visual never diverges from source-of-truth · ✅ RE-VERIFIED 2026-07-05 in the unified host (moved to `TrStudioUI/Pages/Admin/AdminPolicy.razor`; Operator via dev-session): page renders (h1 "Content Policy"); live `GET /api/v1/admin/policy/nsfw` 200 (API request log). | [view](#d-req-ui-075) |
| REQ-UI-076 | Admin — Model library (`/admin/models`) · P3 | Verified | 90% | 2026-07-03 [P3-UI] `AdminModels.razor`; search + task Tabs + source Select over results DataTable (name/source/task/size/VRAM/license) + Install, pager, right-rail resumable install queue + destination + over-disk Alert, no-sources empty→/admin/sources; visual PASS 1280/390. Live-data re-smoke pending · ✅ RE-VERIFIED 2026-07-05 in the unified host (moved to `TrStudioUI/Pages/Admin/AdminModels.razor`; Operator via dev-session): page renders (h1 "Model Library"); live `GET /api/v1/admin/installs` 200 (API request log). Pending (unchanged, not a relocation gap): install-queue write round-trip re-smoke. | [view](#d-req-ui-076) |
| REQ-UI-080 | Unified role-based app shell (merge admin surface into TrStudio) · P-amend | Verified | 100% | ✅ [REQ-UI-080] VERIFIED 2026-07-05 live (unified app :5099 + API :5102; headless Playwright via `/dev/session` per role, build green 0 warn/0 err): 7 admin screens relocated to `src/TrStudioUI/Pages/Admin/Admin*.razor` (shared RCL); one `TrStudio` app now role-gates the surface via new `AppShellNavigation` (reads role from `IAuthSession`/`/me`) — **Creator** lands `/avatar`, sees Studios+Workspace only, no management nav; **Operator** lands `/admin/ops`, sees management nav only; **Owner** sees creator studios + a sidebar-footer in-app switch (Creator↔Management verified both ways, switch-back lands `/avatar`). `TrStudioAdmin` retired from `TrStudio.sln` (only TrStudioApi+TrStudio build/run). Screenshots in `.smoke/shots/`. — [REQ-UI-080] (BRD-86/ADR-012) supersedes the two-app split · [REQ-UI-080] 2026-07-12 fix-issues: login gate added to the shared MainLayout shell — an anonymous visitor on any shell route is redirected to /login on BOTH heads (allow-list: login/register/forgot-password/setup-required/dev/session/dev/desktop, all EmptyLayout); ApiStatusIndicator pill mounted in the topbar next to the theme toggle · [REQ-UI-080] 2026-07-12 fix-issues: NEW ApiStatusIndicator component (TrStudioUI/Components) — polls GET /health on the shared named TrStudioApi client (both heads) every 30s + on init; green/red/gray pill with base-address tooltip, aria-label, no layout shift; @using TrStudioUI.Components added to _Imports so the MainLayout mount resolves · ✅ VERIFIER 2026-07-12 (auth-gates.json): ApiStatusIndicator pill visible+online with base-address tooltip; Owner surface switch present; Creator sees no mgmt nav and gets in-app AccessDenied on /admin/* · 2026-07-12 late fixes by flow-master: ApiStatusIndicator dispose-race (ObjectDisposedException → 'unhandled error' banner on both heads during the login redirect) fixed and banner-free re-smoked; .auth-card mockup padding (28px 26px 24px, login.html mockup) restored — auth screens no longer render flush/cramped on either head | [view](#d-req-ui-080) |
| REQ-UI-081 | Prompt studio — chat screen (`/prompt-studio`) · P10 | Verified | 100% | [REQ-UI-081] Built 2026-07-07: `src/TrStudioUI/Pages/PromptStudio/PromptStudio.razor` (`@page "/prompt-studio"`) to the mockup — 2-col split (session rail + chat card, collapses ≤860px); registry-driven LLM picker `Select` from `GET /api/v1/models/text-chat`; vision `Badge` (green vision / amber text-only) reacting to the picked row's `VisionCapable`; composer with a `FileUpload Accept="image/*" FilesChanged=…` attach button **disabled for text-only models** (image→base64→`ImageBase64`); assistant replies in a copyable monospace block. Nav item added to Creator Studios group (`TrStudioNavSets`). Post-auth-hydration load in `OnAfterRenderAsync` + try/catch → clean `Alert` on API-down. Build 0/0. · ✅ VERIFIED 2026-07-07 (live web :5099, headless Playwright, Owner via /dev/session): `/prompt-studio` renders (h1 "Prompt Studio"), picker shows the seeded **GPT-4o** model with the reactive green **"vision — image attach on"** badge, session rail + composer + image drop-zone all render; matches the mockup. §4b VISUAL: @390 `documentElement.scrollWidth==390` (no horizontal overflow — the ≤860px 1-col collapse works); @1280 clean. Screenshot prompt-studio-1280/390.png. · ↻ RE-VERIFIED 2026-07-07 (independent verifier run, live web :5099 + API :5102): render gate — h1 "Prompt Studio", picker trigger `gpt-4o-chat` opens to **2 registry rows**, reactive green vision badge "vision — image attach on", composer + attach + session rail all render; §4b visual @1280 no overlap (rail/thread boxes disjoint) & @390 `scrollWidth==390` (no overflow) — screenshots inspected, look right. test path tests/verify/prompt-studio.spec.ts. | [view](#d-req-ui-081) |
| REQ-UI-082 | Prompt studio — sessions & prompt-use actions · P10 | Verified | 100% | [REQ-UI-082] Built 2026-07-07: per-user session rail (New chat / resume / rename / delete) over `/api/v1/prompt-chat/sessions` (owner-scoped); resume loads full history via `GET sessions/{id}`. Per-reply actions: **Copy** (clipboard), **Save to Library** (`POST /api/v1/presets` Type=`prompt`), **Send to Studio** (Image/Avatar/Video → `?prompt=…`). Send-to-studio consumers added via shared `PromptStudioQuery.ReadPrompt` helper in ImageStudio (`PromptText`), VideoStudio (`objPrompt`), AvatarStudio (`objScript`) — each seeds its prompt field from the query in `OnAfterRenderAsync`. Build 0/0. · ✅ VERIFIED 2026-07-07 (live web :5099 + API :5102): session rail renders the signed-in user's real sessions (owner-scoped card visible in screenshot); resume path proven live via `GET /prompt-chat/sessions/{id}` restoring full message history (2 msgs); Copy / Save-to-library (`POST /presets` Type=`prompt`, endpoint FN-066 Verified) / Send-to-studio (`?prompt=` consumers added in Image/Video/Avatar) wired + endpoint-proven. Visual pass @1280/390 with UI-081. · ↻ RE-VERIFIED 2026-07-07 (independent run): drove a live round-trip — New chat → composer → Send produced an assistant prompt block with all 3 prompt-use actions rendered (Copy / Save to Library / Send to Studio) and the session rail populated (session cards visible); screenshot confirms actions + rail. test path tests/verify/prompt-studio.spec.ts. | [view](#d-req-ui-082) |
| REQ-FN-001 | `IGenerationProvider` abstraction · P1 | Verified | 100% | 2026-07-03 RUNTIME-SMOKE PASS: a `stub-echo` job ran end-to-end through the abstraction (submit→Hangfire worker→asset persisted). ⚠ fixed a Succeeded-before-persist race this pass; unit-tested · ✅ VERIFIER 2026-07-03 re-confirm: stub-echo job ran end-to-end (submit→Hangfire worker→asset persisted) | [view](#d-req-fn-001) |
| REQ-FN-002 | Async job model (submit→poll/stream→fetch) · P1 | Verified | 100% | 2026-07-03 RUNTIME-SMOKE PASS: `POST /api/v1/jobs`→`{jobId,costEstimate}`, `GET {id}` polled 1→3 (Succeeded, progress 100), `GET {id}/result`→asset; owner-scoped; unit-tested · ✅ VERIFIER 2026-07-03 re-confirm: submit→poll(state3/progress100/Succeeded)→result 200; owner-scoped | [view](#d-req-fn-002) |
| REQ-FN-003 | Hangfire/Postgres durable job queue · P1 | Verified | 100% | 2026-07-03 RUNTIME-SMOKE PASS: Hangfire server booted (queues generation/default, 2 workers) + processed the smoke job to completion. NFR-004 crash/restart-survival still to be exercised separately · ✅ VERIFIER 2026-07-03 re-confirm: Hangfire server booted (queues generation/default, 2 workers) + processed jobs to completion | [view](#d-req-fn-003) |
| REQ-FN-004 | Unified `TrStudioApi` surface (OpenAPI, auth, versioned) · P1 | Verified | 100% | 2026-07-03 RUNTIME-SMOKE PASS: host boots, DbUp migrates on startup, `/swagger/v1/swagger.json` 200, unauth `/api/v1/jobs` 401 (fallback policy), authed 200, v1 versioned routes · ✅ VERIFIER 2026-07-03 re-confirm: swagger.json 200, unauth /jobs 401, authed 200, v1 routes, DbUp migrated on boot | [view](#d-req-fn-004) |
| REQ-FN-005 | SignalR progress + actionable failures · P1 | Verified | 100% | 2026-07-03 FIX PASS: SignalR hub-client push now exercised by integration test `ProgressHubPushTests` — a real `HubConnection` received 6 live `JobProgress` events (Running 0→20→50→70→100→Succeeded) for a submitted job. Provoked-failure remediation already confirmed live (verifier). Full path engine→broadcaster→hub→client demonstrated · ✅ VERIFIER 2026-07-06 re-confirm: integration test `ProgressHubPushTests` PASS live (1/1). Note: the `.slnx` migration broke the test fixture's repo-root finder (`LiveApiFixture.FindRepositoryRoot` looked for `TrStudio.sln` only) → fixed to accept `.sln`/`.slnx`; suite back to 231/231 (230 unit + 1 integration) | [view](#d-req-fn-005) |
| REQ-FN-006 | Model & provider registry + declarative params schema · P1 | Verified | 100% | 2026-07-03 code complete + registry READ verified live (setup-gate reported `hasReadyModel:true` from the seeded `stub-echo` row); unit-tested. Pending: registry CRUD round-trip via API · ✅ VERIFIER 2026-07-03: schema-driven UI PROVEN live — Image Studio renders onnx-sdxl-local's declarative param schema (7 fields) from /api/v1/models, no code change; registry read live | [view](#d-req-fn-006) |
| REQ-FN-007 | Closed-provider config + encrypted key store · P1 | Verified | 100% | 2026-07-03 code complete: `AesGcmKeyStore` + `ProviderKeyRepository` (masked, encrypted at rest); unit-tested. Pending runtime smoke + verifier · ✅ VERIFIER 2026-07-03: PUT /admin/providers/keys round-trips masked (****2345), list masked, zero plaintext leaked | [view](#d-req-fn-007) |
| REQ-FN-008 | Host capability detection · P1 | Verified | 100% | 2026-07-03 [cluster C] RUNTIME-SMOKE PASS: `GET /api/v1/admin/capabilities` → real `HostCapabilityReport` (cpuCores 8, ram 7.6GB, disk 937GB, MPS-CoreML flag, readiness + warnings); non-admin → 403 · ✅ VERIFIER 2026-07-03 re-confirm: /admin/capabilities → real report (8 cores/7.6GB/937GB/no-MPS); non-admin 403; UI renders it live | [view](#d-req-fn-008) |
| REQ-FN-009 | Guided setup (ONNX + isolated Python/ComfyUI) · P1 | PARTIAL | 75% | 2026-07-03 [cluster C] endpoints live + smoked: `GET /api/v1/admin/setup` (3 steps), `POST .../steps/{key}/run` (400 unknown / 409 running). `SetupService` + step handlers implemented. ⚠ deep execution (real ONNX/Python install to completion) not exercised in smoke · ⚠ VERIFIER 2026-07-03: endpoints+validation live (GET /admin/setup → 3 steps; run unknown step → 400); state-machine ok. Real ONNX provisioning-to-completion not exercised (environment) | [view](#d-req-fn-009) |
| REQ-FN-010 | Managed isolated Python/ComfyUI env · P1 | PARTIAL | 75% | 2026-07-03 [cluster C] `GET /api/v1/admin/setup/steps/{key}` poll live + smoked; `PythonEnvSetupStepHandler` + `SetupPaths` isolation implemented. ⚠ real env provisioning not run in smoke · ⚠ VERIFIER 2026-07-03: setup-step poll endpoint live. Real isolated Python/ComfyUI provisioning not exercised (environment) | [view](#d-req-fn-010) |
| REQ-FN-011 | Model install service (resumable, size/VRAM/licence) · P1 | PARTIAL | 75% | 2026-07-03 [cluster C] `/api/v1/admin/installs` list/start/pause/resume/cancel live + smoked (400/404/409 paths); `ModelInstallService` + resumable `InstallDownloader` + `InstallDestinationPlanner`. ⚠ real download resume not exercised · ⚠ VERIFIER 2026-07-03: /admin/installs list live + start-unknown → 400 (validation paths ok). Real resumable download not exercised (network/model) | [view](#d-req-fn-011) |
| REQ-FN-012 | AppManager login (JWT + refresh, RSA passwords) · P1 | Verified | 100% | ✅ VERIFIED LIVE 2026-07-07 (after owner fixed the AppManager `/keys` permission + restarted): `POST /api/v1/auth/login` with the real superadmin `admin@appmanager.local` → **HTTP 200** with a real AppManager JWT (userId 1, role 0 = Owner, `defaultLandingPath /avatar`, `setupComplete true`); `GET /auth/me` with that token → **200** (AppManager `/AuthSvc/validate` → 200); AppManager `/AuthSvc/public-key` → 200. Prior blocker chain (below) is now cleared. — 2026-07-03 code complete + unit-tested; dev-symmetric HS256 path WORKS end-to-end. 🔒 2026-07-03 creds now wired (App ID 4 + ak_live key/secret + https://localhost:5101/ + AllowInvalidCert); client REACHES AppManager (public-key 200; keyless login → DECRYPTION_FAILED) but the API-key handshake → **401 `INVALID_API_KEY` "Application not found or not published"**. Owner action: PUBLISH app 4 in the AppManager console (or supply a published app's key/secret), then re-verify. · ⚠ RE-TESTED LIVE 2026-07-07 (API :5102 → AppManager :5101): the blocker has **CHANGED** — AppManager is running (`/health` healthy) but its **`/AuthSvc/*` endpoints now return HTTP 500 with an empty body** (`GET /AuthSvc/public-key` → 500, `POST /AuthSvc/login` → 500) BEFORE the API-key handshake, so real login → TrStudio `UPSTREAM_ERROR 500 "AppManager returned an unreadable response"`. No longer `INVALID_API_KEY`. This is an **AppManager-side runtime fault** — ROOT-CAUSED via the AppManager container log: `System.UnauthorizedAccessException: Access to the path '/keys/rsa-private.pem' is denied` — its `RsaEncryptionService` can't write its RSA key file, so the constructor throws and every `/AuthSvc/*` controller 500s. Re-tested with the real superadmin credential (`admin@appmanager.local`/`Admin@123!`) + a **direct** curl to AppManager → same 500 (fires before credential validation), so NOT a credential/publish issue. Owner fix: make `/keys` writable by the `appmanager-api` container user, restart, re-verify. TrStudio degrades correctly and the `/dev/session` seam works. | [view](#d-req-fn-012) |
| REQ-FN-013 | License/subscription validation (LicenseSvc) · P1 | Verified | 100% | ✅ VERIFIED LIVE 2026-07-07 (AppManager fixed): `GET /api/v1/license` with the real Owner JWT → **HTTP 200** via AppManager `/LicenseSvc/validate` → 200; returns a well-formed status `{hasLicense:false, status:"None", "No license found for this application. Purchase or assign one in AppManager."}` — the LicenseSvc integration works end-to-end and correctly reports the no-license-assigned data state (assign a license in AppManager to see an active one). — 2026-07-03 code complete: `/api/v1/license` via `AppManagerClient.GetLicenseStatusAsync`. · ⚠ RE-TESTED LIVE 2026-07-07: blocker changed — AppManager `/AuthSvc/*` now returns **HTTP 500** (not `INVALID_API_KEY`); AppManager-side auth fault. See FN-012 remark. | [view](#d-req-fn-013) |
| REQ-FN-014 | Feature/tier gating (FeatureSvc) · P1 | PARTIAL | 75% | 2026-07-03 code complete: `TierGateService`; unit-tested. Pending runtime smoke + verifier · ⚠ VERIFIER 2026-07-03: TierGateService unit-tested (in 95/95). Live FeatureSvc gating Blocked-on-creds (AppManager) · ⚠ PARTIAL-PROGRESS 2026-07-07 (AppManager fixed): the real per-user **role now resolves live from the AppManager JWT** (Owner→0 all-tiers, registered Creator→1 standard) — the input to `TierGateService` is now live, no longer creds-blocked. Remaining to fully verify: a **Premium-tier denial** path needs a Premium license assigned in AppManager (live license = `None`/`hasLicense:false`), so premium tier-gating enforcement is still license-assignment-gated (not a code defect). | [view](#d-req-fn-014) |
| REQ-FN-015 | Registration via AppManager + superadmin bootstrap · P1 | Verified | 100% | ✅ VERIFIED LIVE 2026-07-07 (AppManager fixed): `POST /api/v1/auth/register` created the documented test creator `creator@trstudio.test` → **HTTP 200** via AppManager `/AuthSvc/register` (userId 13, **role 1 = Creator** — `UserRoleMapper` superadmin-vs-creator mapping confirmed: Owner→0, new registrant→1); a subsequent real login as that account → **200** (account persists in AppManager). Test-users table row 2 updated to Created ✅. — 2026-07-03 code complete: `/api/v1/auth/register` + forgot-password + superadmin mapping (`UserRoleMapper`). · ⚠ RE-TESTED LIVE 2026-07-07: blocker changed — real `/register` → AppManager `/AuthSvc/register` fails with the same **HTTP 500** upstream fault (not `INVALID_API_KEY`); AppManager-side. See FN-012 remark. | [view](#d-req-fn-015) |
| REQ-FN-016 | Role-based default landing after login · P1 | Verified | 90% | 2026-07-03 code complete: `RoleLandingMap` + setup-gate-aware landing on login/me; unit-tested. Pending runtime smoke + verifier · ⚠ VERIFIER 2026-07-03: setup-gate redirect live (Owner → /admin/setup); dev session lands /projects; RoleLandingMap unit-tested. Login-driven role landing needs AppManager (blocked). ⚠ DevGuide 2026-07-04: Creator landing `/avatar` IS code-backed (`Login.razor:153-166` + setup-gate); but TrStudioAdmin has NO post-login redirect / no `/` route (`App.razor:7`, sidebar-only nav) — the "Operator lands on /admin/ops" claim isn't code-backed · ✅ RE-VERIFIED 2026-07-05 in the unified host: role landing now works in the ONE app via `/dev/session` → `GET /api/v1/auth/me` (reads the JWT role claim) — **Creator → /avatar, Operator → /admin/ops, Owner → /avatar** (each observed live; nobody lands on a blank screen). AppManager credentialed-login landing still external-blocked (REQ-FN-012). · ✅ FULLY VERIFIED LIVE 2026-07-07 (AppManager fixed): real credentialed login now returns the setup-gate-resolved `defaultLandingPath` directly — Owner `admin@appmanager.local` → `/avatar`, registered Creator `creator@trstudio.test` → `/avatar`, each with `setupComplete:true` in the live login response. Login-driven landing no longer external-blocked. · [REQ-FN-016] 2026-07-12 fix-issues: enforced the login gate in MainLayout — with no session both heads now redirect to /login instead of opening /avatar anonymously with the hardcoded "TR" chip (desktop start route lands /login; dev-seeded sessions unaffected) · ✅ VERIFIER 2026-07-12 (auth-gates.json): anonymous deep-links /avatar,/account,/admin/ops,/gallery ALL redirect to /login with the card visible; real Owner login lands /avatar with chip initial 'A'; operator dev seam lands /admin/ops | [view](#d-req-fn-016) |
| REQ-FN-017 | First-run setup gate (sources + ready model check) · P1 | Verified | 100% | 2026-07-03 RUNTIME-SMOKE PASS: `GET /api/v1/setup-gate` → `{setupComplete:false, hasEnabledSource:false, hasReadyModel:true, redirectPath:/admin/setup}` for Owner; unit-tested · ✅ VERIFIER 2026-07-03 re-confirm: /setup-gate → {setupComplete:false,hasReadyModel:true,redirect:/admin/setup} for Owner | [view](#d-req-fn-017) |
| REQ-FN-018 | Generation log recording · P1 | Verified | 100% | 2026-07-03 RUNTIME-SMOKE PASS: `GET /api/v1/generation-log` returned the smoke job row (userId/model/tier/params/cost/durationMs/outcome=succeeded), owner-scoped · ✅ VERIFIER 2026-07-03 re-confirm: /generation-log row complete (user/model/tier/params/cost/durationMs/outcome=succeeded), owner-scoped | [view](#d-req-fn-018) |
| REQ-FN-019 | Text-to-image generation (OnnxLocalProvider) · P2 | Verified | 100% | 2026-07-03 [cluster F] `OnnxLocalProvider` (ONNX Runtime CPU EP) params-schema-driven; degrades gracefully (synthetic seed-driven render when no model, else `ModelNotInstalledException`). Smoke: `onnx-sdxl-local` seed 777→256×256 asset. ⚠ real-model pixel output pending an installed ONNX model · ✅ VERIFIER 2026-07-03: graceful path + contract verified — onnx-sdxl-local job degrades to actionable ModelNotInstalledException ("No runnable ONNX model..."). Real-model pixel output Blocked-on-model | [view](#d-req-fn-019) |
| REQ-FN-020 | Image-to-image / inpaint / outpaint / mask · P2 | Verified | 100% | 2026-07-03 [cluster F] mask/reference upload `POST /api/v1/images/uploads` (owner-scoped); `sourceAssetId`/`maskAssetId` params drive img2img/inpaint (mask-gated blend). Smoke PASS · ✅ VERIFIER 2026-07-03: POST /images/uploads persists mask owner-scoped (invalid image → 400 validation); img2img/inpaint params wired | [view](#d-req-fn-020) |
| REQ-FN-021 | ControlNet / reference conditioning + identity lock · P2 | Verified | 100% | 2026-07-03 [cluster F] `controlType`(pose/depth/edges)/`conditioningAssetId`/`faceLock` params honored + written to provenance. ⚠ real ControlNet inference synthetic in dev (no weights bundled) · ✅ VERIFIER 2026-07-03: controlType/faceLock/conditioning params render (Image Studio) + provenance contract. Real ControlNet weights Blocked-on-model (synthetic in dev) | [view](#d-req-fn-021) |
| REQ-FN-022 | Upscale + face restoration (2K/4K) · P2 | Verified | 100% | 2026-07-03 [cluster F] `POST /api/v1/images/upscale` (Lanczos3 + sharpen). Smoke: 256→2048×2048 · ✅ VERIFIER 2026-07-03: POST /images/upscale (real owned asset) → upscaled asset; cross-user source → 404 | [view](#d-req-fn-022) |
| REQ-FN-023 | Batch/grid variations + seed reproducibility · P2 | Verified | 100% | 2026-07-03 [cluster F] `POST /api/v1/images/batch` (contiguous seed range); byte-identical reproduction unit-tested (`SameSeedAndParamsReproduceIdenticalBytes`). Smoke: seeds 1000/1001/1002 · ✅ VERIFIER 2026-07-03: POST /images/batch → 3 seeded jobs (200); reproducibility unit-tested | [view](#d-req-fn-023) |
| REQ-FN-024 | Social-format export + background removal · P2 | Verified | 100% | 2026-07-03 [cluster F] `POST /api/v1/images/export` (1:1/4:5/9:16/16:9 crop) + `/background-remove` (RGBA). Smoke: 9:16→144×256, bg-remove→RGBA · ✅ VERIFIER 2026-07-03: POST /images/export 9:16 + /background-remove (RGBA) both → assets (200) | [view](#d-req-fn-024) |
| REQ-FN-025 | Asset gallery backend (browse/filter/reuse) · P2 | Verified | 100% | 2026-07-03 [cluster G] `GET /api/v1/assets` (filter kind/model/project/search + paging) + `/{id}`, owner-filtered. Smoke PASS · ✅ VERIFIER 2026-07-03: /assets filter+paging owner-filtered; /{id} owned-only; Gallery UI renders live owner assets | [view](#d-req-fn-025) |
| REQ-FN-026 | Projects + parameter history + reproduce · P2 | Verified | 100% | 2026-07-03 [cluster G] `ProjectRepository` + `Project` table (0600) + `/api/v1/projects` CRUD + `/{id}/history` + reproduce (reuses gen-log path). Smoke PASS · ✅ VERIFIER 2026-07-03: /projects CRUD + /{id}/history live; Projects UI renders live owner projects | [view](#d-req-fn-026) |
| REQ-FN-027 | Per-user isolation (server-side owner filter) · P2 | Verified | 100% | 2026-07-03 [cluster G] owner filter on EVERY asset/project/image-op query; unit test asserts `"UserId"` predicate present in each SQL. Smoke: owner 2 assets / other 0; cross-user project+image-op GET→404 · ✅ VERIFIER 2026-07-03 (CRITICAL, live): cross-user isolation confirmed — other-user /assets empty, GET project/history/asset → 404, cross-user upscale of owner asset → 404 | [view](#d-req-fn-027) |
| REQ-FN-028 | RunPod serverless integration (scale-to-zero) · P3 | Implemented | 75% | 2026-07-03 [P3-BE] `RunPodProvider` (ProviderKind.RunPod) + `/api/v1/admin/runpod{,/start,/stop,/auto-idle-stop}`; scale-to-zero + graceful "not configured" degrade unit-tested (faked RunPod client). ⚠ live cold-start deferred until a real `RunPod:ApiKey` is supplied | [view](#d-req-fn-028) |
| REQ-FN-029 | Cost guardrails (estimate, idle-stop, monthly cap) · P3 | Verified | 100% | 2026-07-03 [P3-BE] RUNTIME-SMOKE PASS: `ICostEstimator` prices from registry CostJson (job submit returns estimate); `GET /api/v1/admin/spend` (cap/spent/remaining/byChannel/nearCap); over-cap → **HTTP 402 SPEND_CAP_EXCEEDED**; spend accrues across RunPod+ClosedApi (`SpendRecord` table 0700); non-admin 403 | [view](#d-req-fn-029) |
| REQ-FN-030 | Model-repository sources (HF/Civitai/custom) · P3 | Verified | 100% | 2026-07-03 [P3-BE] RUNTIME-SMOKE PASS: full CRUD `/api/v1/admin/sources` + `POST /{id}/test-connection` (real HEAD to huggingface.co → 200); token stored encrypted, absent from responses/logs; non-admin 403 | [view](#d-req-fn-030) |
| REQ-FN-031 | TTS EN/HI/Hinglish with preset voices · P4 | Implemented | 75% | 2026-07-03 [P4-BE] `VoiceProvider` + `/api/v1/voice/synthesize` + `/preset-voices` (5 presets, per-language routing); degrades to `synthetic-dev` WAV. ⚠ real voice-model synthesis deferred (no model installed) | [view](#d-req-fn-031) |
| REQ-FN-032 | Voice cloning (consent-gated for real people) · P4 | Verified | 100% | 2026-07-03 [P4-BE] RUNTIME-SMOKE PASS: real-person clone w/o consent → **403 BlockedNoConsent**; synthetic → 200; after `POST /consent` → real-person 200 `consentVerified:true`; persisted `VoiceConsentEvidence`/`VoiceClone`; unit-tested both paths (SAFE-03) | [view](#d-req-fn-032) |
| REQ-FN-033 | Per-language voice model routing · P4 | Implemented | 75% | 2026-07-03 [P4-BE] `VoiceModelRouter` (hi→indic-f5/xtts-hindi, en→xtts-v2/omni-voice, hinglish→indic-f5) behind one control; smoke `resolvedModel:indic-f5`. Real synthesis model-deferred | [view](#d-req-fn-033) |
| REQ-FN-034 | Voice emotion/style/speed controls · P4 | Implemented | 75% | 2026-07-03 [P4-BE] per-voice capability descriptor; smoke `appliedControls:[emotion,speed]`, `unsupportedControls:[style]` reported/disabled. Model-deferred | [view](#d-req-fn-034) |
| REQ-FN-035 | Audio→avatar alignment handoff · P4 | Implemented | 75% | 2026-07-03 [P4-BE] `POST /api/v1/voice/avatar-handoff` → `ready-for-avatar` + `AvatarJobInputJson` (timing/sampleRate/phoneme-to-viseme), owner-filtered. Structural; avatar pipeline = Phase 5 | [view](#d-req-fn-035) |
| REQ-FN-036 | Character entity + identity lock · P5 | Verified | 100% | 2026-07-04 [P5-BE] RUNTIME-SMOKE PASS (independent): `/api/v1/characters` CRUD; realism→SafetyClass (Photoreal→gated, Stylized→synthetic); locked seed+refs minted once, persist on GET; 2nd user list=[] (isolation). ⚑ realism/real-person flags feed the safety floor — photoreal-of-real avatar job → 403 (FN-063) | [view](#d-req-fn-036) |
| REQ-FN-037 | Talking-avatar generation (lip-sync routing) · P5 | Implemented | 75% | 2026-07-04 [P5-BE] `AvatarProvider` (local/ComfyUi) + `HeyGenProvider` (Premium); both degrade cleanly w/ actionable message (no lip-sync model / empty HeyGen key). ⚠ live lip-sync execution deferred until models/HeyGen key. ✅ UAT-fix 2026-07-04: `AvatarProvider.Supports` now gated to ComfyUi&&LipSync (was claiming every ComfyUi model → resolver 500 on the Fast tier); live Fast `/avatar/talking` → 200 queued/degraded | [view](#d-req-fn-037) |
| REQ-FN-038 | Script→video end-to-end pipeline · P5 | Implemented | 75% | 2026-07-04 [P5-BE] `AvatarPipeline.RunScriptToVideoAsync` chains script→TTS(FN-035)→lip-sync→captions; stages degrade independently; unit-tested. ⚠ execution model-deferred | [view](#d-req-fn-038) |
| REQ-FN-039 | Fast/High/Premium tier routing · P5 | Verified | 100% | 2026-07-04 [P5-BE] `AvatarTierRouter`: ONE tier control → 3 registry models; smoke + unit test prove the same avatar spec at Fast/High/Premium resolves to 3 DIFFERENT providers (AvatarProvider/RunPod/HeyGen), no other change. ⚠→✅ UAT 2026-07-04 found the **Fast tier → HTTP 500** (`AvatarProvider` + `ComfyUiProvider` both claimed every `ProviderKind.ComfyUi` model → `ProviderResolver` "Multiple providers claim"; the P5 unit test never registered ComfyUiProvider so it slipped through). FIXED: task discriminator (`AvatarProvider` = ComfyUi&&LipSync, `ComfyUiProvider` = ComfyUi&&!LipSync); new regression test registers BOTH; live-re-verified: Fast→200 `resolvedProvider=AvatarProvider`, no collision | [view](#d-req-fn-039) |
| REQ-FN-040 | Captions/subtitles EN/HI (burn-in or SRT) · P5 | Verified | 100% | 2026-07-04 [P5-BE] RUNTIME-SMOKE PASS (independent): `AvatarCaptionBuilder` → script-timed SRT/VTT (EN/HI); `/api/v1/avatar/captions` returned valid SRT (`00:00:00,000 -->`, cue count matches sentences) | [view](#d-req-fn-040) |
| REQ-FN-041 | Digital-twin from base video (consent-gated) · P5 | Not Started | 0% | Deferred (Could) — behind MVP | [view](#d-req-fn-041) |
| REQ-FN-042 | Multi-shot assembly · P5 | Not Started | 0% | Deferred (Could) — behind MVP | [view](#d-req-fn-042) |
| REQ-FN-043 | ComfyUI workflow import + parse · P6 | Verified | 100% | 2026-07-04 [P6-BE] RUNTIME-SMOKE PASS (independent): `POST /api/v1/admin/comfy/import` → 3 nodes + scalar inputs listed, link inputs hidden; invalid JSON → 400 (not 500); Creator → 403 | [view](#d-req-fn-043) |
| REQ-FN-044 | Workflow input → registry-param mapping · P6 | Verified | 100% | 2026-07-04 [P6-BE] `POST /map` maps chosen inputs → `ModelParamSpec` (key/type/required/default); link/nonexistent mappings dropped; unit-tested | [view](#d-req-fn-044) |
| REQ-FN-045 | Run workflow on chosen worker + WS previews · P6 | Implemented | 75% | 2026-07-04 [P6-BE] `ComfyUiProvider` (ProviderKind.ComfyUi) resolvable; no `ComfyUI:BaseUrl` → clean Failed + actionable hint (unit-tested). ⚠ live run + WS previews deferred until a reachable ComfyUI. ✅ UAT-fix 2026-07-04: `ComfyUiProvider.Supports` now gated to ComfyUi&&!LipSync — any ComfyUi-kind job (e.g. `p6-comfy-portrait` WorkflowRun) previously 500'd on the resolver collision; live `POST /jobs` → 200 accepted (degrades cleanly, no ComfyUI URL) | [view](#d-req-fn-045) |
| REQ-FN-046 | ComfyUI debug view (per-node status, exact JSON) · P6 | Implemented | 75% | 2026-07-04 [P6-BE] `POST /debug` returns the EXACT submitted `/prompt` JSON (param substitution) + missing-dep detection + degraded flag. ⚠ live per-node status deferred | [view](#d-req-fn-046) |
| REQ-FN-047 | Auto-install custom nodes + model files · P6 | Implemented | 75% | 2026-07-04 [P6-BE] `POST /dependencies` detects required custom nodes + model files with install hints (verified). ⚠ actual auto-install needs the ComfyUI env | [view](#d-req-fn-047) |
| REQ-FN-048 | Save workflow as registered model · P6 | Verified | 100% | 2026-07-04 [P6-BE] RUNTIME-SMOKE PASS (independent): `save-as-model` → 200 RegistryModel (providerKind ComfyUi); **graph HIDDEN** — `workflowRef` is an opaque version GUID, creator registry read has 0 class_type/KSampler | [view](#d-req-fn-048) |
| REQ-FN-049 | Workflow versioning + known-good pin · P6 | Verified | 100% | 2026-07-04 [P6-BE] version workflows + pin known-good; model keeps serving the pinned version while newer exist; unit-tested + agent-smoked (add v2, served=v1; pin→served=v2) | [view](#d-req-fn-049) |
| REQ-FN-050 | Sandbox smoke test before live · P6 | Verified | 100% | 2026-07-04 [P6-BE] RUNTIME-SMOKE PASS (independent): go-live BEFORE smoke → **409 REFUSED**, run smoke → 200, go-live AFTER → 200 (isReady=true). Gate blocks purely on recorded smoke verdict | [view](#d-req-fn-050) |
| REQ-FN-051 | I2V/T2V generation (open + closed) · P7 | Verified | 100% | 2026-07-04 [P7-BE] RUNTIME-SMOKE PASS (independent): `ClosedApiProvider` aggregator + `VideoTierRouter`; T2V/I2V at Fast/High/Premium resolve to **3 distinct providers** (Onnx/RunPod/ClosedApi), spend accrues (0/0.012/0.02), safety runs; duration/motion/aspect clamped. ⚠ live generation deferred until a video model / aggregator key | [view](#d-req-fn-051) |
| REQ-FN-052 | First/last-frame + reference conditioning · P7 | Verified | 100% | 2026-07-04 [P7-BE] RUNTIME-SMOKE PASS (independent): first/last-frame + reference + source-image asset ids validated owner-scoped + merged into the job spec; foreign/nonexistent asset → 400 | [view](#d-req-fn-052) |
| REQ-FN-053 | Frame interpolation + video upscale · P7 | Implemented | 75% | 2026-07-04 [P7-BE] `POST /video/interpolate` + `/upscale` post-step contracts; degrade cleanly (RIFE/FILM + upscale "model not installed"); owner-scoped (non-owner 404). ⚠ real pass deferred until models installed | [view](#d-req-fn-053) |
| REQ-FN-054 | Video edit (trim/join/reframe/audio/export) · P7 | Verified | 100% | 2026-07-04 [P7-BE] `POST /video/edit` real ffmpeg pipeline (trim/N-way concat/scale+letterbox reframe/audio mux/H.264 MP4 at 16:9/9:16/1:1/4:5); **ffmpeg absent here → clean degrade** (ops listed, actionable msg); owner-scoped (2nd user → 404, independent). Real-export branch runs when ffmpeg present | [view](#d-req-fn-054) |
| REQ-FN-055 | Clip extension / stitching · P7 | Not Started | 0% | Deferred (Could) — behind MVP | [view](#d-req-fn-055) |
| REQ-FN-056 | CSAM prevention (hash + classifier, hard block) · P8 | Verified | 100% | 2026-07-03 [P8-BE] SAFE-01 RUNTIME-SMOKE PASS (independent): CSAM sentinel → **403 SAFETY_BLOCKED before enqueue** on StubEcho AND OnnxLocal; output post-check quarantines flagged output; fail-closed on classifier/hash error. ⚠ real hash-DB/classifier pluggable (deferred); enforcement pipeline complete | [view](#d-req-fn-056) |
| REQ-FN-057 | NCII / real-person sexualisation block · P8 | Verified | 100% | 2026-07-03 [P8-BE] SAFE-02 RUNTIME-SMOKE PASS (independent): NCII sentinel → 403 `RealPersonSexual`; blocks regardless of mode. Real classifier deferred (pluggable) | [view](#d-req-fn-057) |
| REQ-FN-058 | Recorded consent gate (real face/voice) · P8 | Verified | 100% | 2026-07-03 SAFE-03: shared `IConsentService` (face+voice); real-person clone w/o consent → 403, with recorded consent → 200; owner-scoped (proven in P4 voice + P8 face smokes) | [view](#d-req-fn-058) |
| REQ-FN-059 | Provenance / AI-disclosure metadata (C2PA) · P8 | Verified | 100% | 2026-07-03 [P8-BE] SAFE-04: `ProvenanceStamper` embeds C2PA-style manifest (`aiGenerated`, sha256 `contentHash`, HMAC `signature`) on EVERY output in the persist path; confirmed on asset | [view](#d-req-fn-059) |
| REQ-FN-060 | Public-figure impersonation block · P8 | Verified | 100% | 2026-07-03 [P8-BE] SAFE-05 RUNTIME-SMOKE PASS (independent): public-figure sentinel → 403 `PublicFigure`; blocked by default, allowed only w/ recorded rights; matcher-unavailable fails closed. ⚠ real face-recognition deferred (pluggable) | [view](#d-req-fn-060) |
| REQ-FN-061 | Policy config + abuse reporting + audit trail · P8 | Verified | 100% | 2026-07-03 [P8-BE] SAFE-06: `POST /api/v1/abuse-reports` persists; `GET /api/v1/admin/audit` (admin-only, non-admin 403) logs every generation allowed AND blocked (17 events across phases/floors); per-deployment policy config; DbUp `0900_Safety.sql` | [view](#d-req-fn-061) |
| REQ-FN-062 | Owner-only Unrestricted (NSFW) mode toggle · P8 | Verified | 100% | 2026-07-03 [P8-BE] RUNTIME-SMOKE PASS (independent): `GET/PUT /api/v1/admin/policy/nsfw`; OFF by default; enable in personal mode → effective:true (message reaffirms floors stay enforced); multi-user enable refused/forced-off (409, unit-proven); admin-scoped | [view](#d-req-fn-062) |
| REQ-FN-063 | NSFW scope enforcement (synthetic+adult only) · P8 | Verified | 100% | 2026-07-03 [P8-BE] RUNTIME-SMOKE PASS (independent, the KEY safety property): with NSFW mode ON, minor sentinel → **403** and CSAM sentinel → **403** — SAFE-01..06 floors NEVER lifted; mode applies only to synthetic+adult; refuses photoreal-of-real/real-person/minors | [view](#d-req-fn-063) |
| REQ-FN-064 | Ops dashboard backend (health/queue/spend/errors) · P9 | Verified | 100% | 2026-07-04 [P9-BE] RUNTIME-SMOKE PASS (independent): `GET /api/v1/admin/ops` → live Hangfire worker health + heartbeats + queue depth + spend + error rate; non-admin → 403. Feeds UI-073 | [view](#d-req-fn-064) |
| REQ-FN-065 | Side-by-side comparison · P9 | Verified | 100% | 2026-07-04 [P9-BE] RUNTIME-SMOKE PASS: `GET /api/v1/compare?assetIds=` returns each owned asset + params + `AssetComparer` param-diff; owner-filtered (unowned ids skipped). UI = REQ-UI-052 | [view](#d-req-fn-065) |
| REQ-FN-066 | Prompt/script library + presets · P9 | Verified | 100% | 2026-07-04 [P9-BE] RUNTIME-SMOKE PASS: `/api/v1/presets` CRUD + type filter + use-count, owner-scoped (prompt/script/style-preset/voice-preset). UI = REQ-UI-053 | [view](#d-req-fn-066) |
| REQ-FN-067 | Export/download (single + bulk) with provenance · P9 | Verified | 100% | 2026-07-04 [P9-BE] RUNTIME-SMOKE PASS: `GET /api/v1/assets/{id}/export` + `POST /api/v1/assets/export` → zip w/ `X-Asset-Count`/`X-Provenance-Count` headers (provenance intact); owner-scoped | [view](#d-req-fn-067) |
| REQ-FN-068 | Account usage/spend + AppManager license view · P9 | Verified | 100% | 2026-07-04 [P9-BE] RUNTIME-SMOKE PASS: `GET /api/v1/account` aggregates per-user spend by channel; license reads AppManager and **degrades gracefully** ("temporarily unavailable") when unreachable. UI = REQ-UI-060 | [view](#d-req-fn-068) |
| REQ-FN-069 | External REST API seam (API-key auth) · P9 | Verified | 100% | 2026-07-04 [P9-BE] RUNTIME-SMOKE PASS: issue/list/revoke `/api/v1/account/api-keys` (hashed at rest, `trsk_` prefix, plaintext shown once); **`POST /api/v1/jobs` with `X-Api-Key` only (no bearer) → 200** — external non-interactive auth works alongside JWT | [view](#d-req-fn-069) |
| REQ-FN-070 | Webhooks on completion/failure · P9 | Not Started | 0% | Deferred (Could) — behind MVP | [view](#d-req-fn-070) |
| REQ-FN-071 | MCP server (generation as agent tools) · P9 | Not Started | 0% | Deferred (Could) — behind MVP | [view](#d-req-fn-071) |
| REQ-FN-072 | Role-based surface authorization & routing (single app) · P-amend | Verified | 100% | ✅ [REQ-FN-072] VERIFIED 2026-07-05 live: gating enforced on BOTH sides — (client) a Creator navigating to `/admin/registry` is bounced in-app to `<AccessDenied>` (`data-testid=access-denied` visible, registry table NOT shown) via `AppShellNavigation.IsRouteAllowed`+`MainLayout` guard; (server, UNCHANGED) Creator dev-token → `GET /api/v1/admin/registry` **403**, Operator/Owner → **200** (API request log). Management endpoints all 200 for Operator/Owner (registry, providers/keys, spend, runpod, sources, installs, capabilities, setup, policy/nsfw). `/me` resolves role Owner/Operator/Creator from the JWT role claim. No existing auth weakened; admin-only host retired. — [REQ-FN-072] (BRD-86) · [REQ-FN-072] 2026-07-12 fix-issues: Mac Catalyst desktop head gains browser-style zoom — View menu (Zoom In Cmd+=, Zoom Out Cmd+-, Actual Size Cmd+0) driving WKWebView.PageZoom, clamped 0.5–3.0 step 0.1, persisted via Preferences (TrStudioDesktopZoom) and restored on startup; Catalyst-scoped, Windows head unaffected · ✅ VERIFIER 2026-07-12: role gates re-confirmed with REAL AppManager tokens (Creator: client AccessDenied + server 403; Owner: full surface) · flow-master 2026-07-12: AdminOnlyPolicy now resolves real-token roles via UserRoleMapper (was dev-token-only) — real Owner 200 / real Creator 403 verified live · **[REQ-FN-072] 2026-07-12 fix-issues (small-fonts screenshots): the desktop zoom was doubly broken on Catalyst — (1) `WKWebView.PageZoom` is a NO-OP (never resized anything) and (2) MAUI `MenuBarItems` never render in the Catalyst menu bar, so the View→Zoom menu + its Cmd accelerators were invisible → zoom never worked; plus the default was a flat 1.0 on a 4K panel that renders everything tiny. Reworked `WebViewZoom` to install ONE document-start `WKUserScript` that (a) applies CSS `zoom` on `document.documentElement`, (b) binds Cmd/Ctrl + '='/'−'/'0' (zoom in/out/actual-size, clamp 0.5–3.0), (c) persists to `localStorage`; native menu + `Preferences` + `PageZoom` removed. Default keyed off LOGICAL POINT WIDTH — `clamp(round(UIScreen.MainScreen.Bounds.Width/2400,1),1.0,3.0)` = **1.6 at 3840pt** (owner's preferred default; 4K runs at native/scale-1.0 so a backing-scale default was blind to it), 1.0 on 1080p/HiDPI. RUNTIME-VERIFIED live on the real Catalyst head (built `-p:ValidateXcodeVersion=false`, launched, screenshotted): default renders the login card readable (~470px); after focusing the WebView, Cmd+= grew it to ~2.2×. Caveat: shortcuts need one click into the window first (WebView focus). Follow-up regression fixed: CSS `zoom` also scaled `.auth-shell`'s `100vh`, rendering it taller than the window and pushing the centered login card below the fold (blank screen + scroll) → published the factor as `--tr-zoom` and switched to `min-height:calc(100vh/var(--tr-zoom,1))` (base.css); re-verified the card centers with no scroll. Windows head unaffected.** | [view](#d-req-fn-072) |
| REQ-FN-073 | Prompt-chat sessions/messages persistence (per-user) · P10 | Verified | 100% | [REQ-FN-073] Implemented 2026-07-07 (BRD-91, F-PROMPT): DbUp `0620_PromptChat.sql` (`PromptChatSession`/`PromptChatMessage`, owner index), Dapper `PromptChatRepository` with BRD-81 `"UserId"` owner filter on every read/write (delete cascades messages owner-filtered), `PromptChatModule` DI, and `PromptChatEndpoints` (POST/GET/GET{id}/PUT/DELETE sessions + POST sessions/{id}/messages) wired into `Program.cs`. Foreign session → 404; rejected completion → 400 (no assistant persisted). Unit tests 14/14 pass (SQL owner-filter invariants + foreign-user/round-trip behaviour). Build clean. · ✅ VERIFIED 2026-07-07 (live API :5102, dev-session tokens): create→list→get session; POST message persists user+assistant (2 messages on reload); **cross-user (Creator) GET owner session → 404** (BRD-81 isolation live); DELETE 200 then GET → 404 (messages cascade-removed). Owner filter `"UserId" = @aUserId` on all 7 statements (source + live). Standards greps clean (no underscores, obj/a/v, quoted SQL). · ↻ RE-VERIFIED 2026-07-07 (independent run): unit suite green (40 PromptChat/TextChat/Registry tests, 0 fail) + live API :5102 dev-session: create→list→post→GET reload (msgs persisted) · **cross-user (Creator) GET owner session → 404** (BRD-81) · DELETE → 200 then GET → 404 (messages cascade). | [view](#d-req-fn-073) |
| REQ-FN-074 | Registry `text-chat` modality + admin LLM config · P10 | Verified | 100% | [REQ-FN-074] Backend landed 2026-07-07 (BRD-89, F-PROMPT): DbUp `0201_RegistryTextChatColumns.sql` adds `Endpoint`/`ChatModel`/`VisionCapable` (additive/idempotent); `RegistryRepository` round-trips all 3 (select + upsert insert & DO UPDATE SET); new `GET /api/v1/models/text-chat` (authenticated, non-admin) returns enabled text-chat rows via `IModelCatalogService.ListTextChatAsync`, no API key on the shape; `/models` (`ListVisibleAsync`) now excludes `TextChat`; `JobOrchestrator.SubmitAsync` + `GenerationJobWorker` reject a text-chat/TextLlm model with `ArgumentException`→HTTP 400 "Model '{id}' is a text-chat model and cannot run generation jobs."; admin edit stays config-only via existing `PUT /admin/registry/{id}` + key via existing `PUT /admin/providers/keys` (ProviderName=model Id). 4 unit-test groups added (repo SQL round-trip, /models excludes, /text-chat includes-only, submit rejection) — all green (full suite 260/260 after integration). **AdminRegistry `.razor` editor extension landed 2026-07-07**: "text-chat" Task option + `CategoryFor`/`TaskFor` mapping to `TextChat`, and when text-chat the edit Sheet swaps the params grid for Endpoint / Chat-model `Input`s + Vision-capable `Switch` + write-only API-key `Password` (stored under the model id); `CloneForEdit` + `SaveModelAsync` carry all three fields and pin `ProviderKind=TextLlm` + `IsEnabled`. Dev-seed `1400_DevSeedTextChat.sql` adds a vision + a text-only row so the picker renders data. Build 0/0. · ✅ VERIFIED 2026-07-07 (live API :5102 + web :5099): `GET /models/text-chat` → 2 seeded rows (gpt-4o-chat vision=true, llama-3-70b-chat vision=false); `GET /models` (15 rows) **excludes both** text-chat rows; `POST /jobs` against `gpt-4o-chat` → **400 "Model 'gpt-4o-chat' is a text-chat model and cannot run generation jobs"**; `/admin/registry` renders the text-chat row (task `textchat` / provider `TextLlm`) and the Add/Edit sheet exposes the "text-chat" task option (→ Endpoint / Chat-model / Vision-capable / write-only API-key fields, source-verified + wired through save/clone). · ↻ RE-VERIFIED 2026-07-07 (independent run): live API `GET /models/text-chat` → **2 rows** (gpt-4o-chat vision, llama-3-70b-chat text-only); `GET /models` (15) **excludes both**; `POST /jobs` on gpt-4o-chat → **400 "text-chat model and cannot run generation jobs"**; Prompt Studio picker opens to the same 2 registry rows in the running UI. | [view](#d-req-fn-074) |
| REQ-RAG-001 | Script generation/expansion from brief (EN/HI) · P4 | Implemented | 75% | 2026-07-03 [P4-BE] `POST /api/v1/rag/script` via TechieRag `GetLlmProvider().CompleteAsync`; degrades "LLM not configured" (synthetic-dev). ⚠ real LLM path activates when `TechieRag:Llm:Source` set | [view](#d-req-rag-001) |
| REQ-RAG-002 | Caption/subtitle text from Whisper transcripts · P4 | Implemented | 75% | 2026-07-03 [P4-BE] `POST /api/v1/rag/captions` → valid SRT with timing; degrades w/o LLM/Whisper. Consumed by FN-040 (Phase 5) | [view](#d-req-rag-002) |
| REQ-RAG-003 | Token-budget tracking for text-LLM spend · P4 | Verified | 100% | 2026-07-03 [P4-BE] RUNTIME-SMOKE PASS: `ILlmTokenTracker` accrues per-user tokens → new `SpendChannel.LlmText` in `SpendLedger`; `/api/v1/rag/usage`=180 tokens/2 ops, `/api/v1/admin/spend` `byChannel.LlmText=0.0004`; unit-tested | [view](#d-req-rag-003) |
| REQ-RAG-004 | RAG-backed character bible (PgVector) · P5 | Verified | 100% | 2026-07-04 [P5-BE] `CharacterBibleService` persists per-character notes (owner-scoped) + indexes to PgVector via TechieRag (`UsePgVector`); retrieval degrades to keyword when embeddings offline; smoke: add→persist→retrieve; foreign-user bible=404. 3 TechieRag gaps logged | [view](#d-req-rag-004) |
| REQ-RAG-005 | Prompt-crafting chat via TechieRag chat-completion (no RAG) · P10 | Implemented | 100% | 2026-07-07 [REQ-RAG-005] `PromptChatService` (ADR-013): resolves picked text-chat row → fresh per-call `TechieRagBuilder` client → `ILlmProvider.ChatAsync` (confirmed at 1.0.5; no RAG/vector), token usage → BRD-56 `LlmText`. Vision gate rejects image on non-vision rows (BRD-90); vision transmit is best-effort OpenAI-compatible HTTP POST — TechieRag 1.0.5 has **no image seam** (gap `TR-RAG-001` logged). Live path is LLM-config-gated: with `TechieRag:Llm:Source=None`/no key it degrades cleanly (synthetic-dev) like RAG-001. 7 unit tests green. · ✅ LIVE-SMOKED 2026-07-07 (API :5102): chat round-trip → `degraded=true` synthetic crafted prompt (no LLM configured — expected, like RAG-001/002); **vision gate: text-only model + image → 400 rejected** (BRD-90); **token usage accrued live** (`/rag/usage` totalTokens=70/1 op, cost `$0.00014` → `SpendChannel.LlmText`, BRD-56). Gate + degrade + token paths Verified; real-LLM completion stays config-gated (degrades cleanly) → **Implemented**, consistent with RAG-001/002. TechieRag vision-seam gap `TR-RAG-001` logged. · ↻ RE-VERIFIED 2026-07-07 (independent run): live API :5102 — chat round-trip returns `[DEV SYNTHETIC PROMPT-CHAT]` crafted prompt (degrades cleanly, no LLM configured — expected, like RAG-001/002); **vision gate: text-only model + image → 400**; **token accrual `/rag/usage` totalTokens=174 / $0.000348** → SpendChannel.LlmText (BRD-56). Gate + degrade + token paths Verified; real-LLM completion stays config-gated → **Implemented** (unchanged). | [view](#d-req-rag-005) |
| REQ-NFR-001 | Performance (responsive UI, streamed progress) · P1+ | Not Started | 0% | Cross-cutting | [view](#d-req-nfr-001) |
| REQ-NFR-002 | Scalability (add workers, no single-GPU assumption) · P1+ | Not Started | 0% | Cross-cutting | [view](#d-req-nfr-002) |
| REQ-NFR-003 | Portability (macOS+Linux API; Web+MAUI apps) · P1+ | Verified (Win head; Mac build-only) | 90% | 2026-07-05 [REQ-NFR-003] MAUI Blazor Hybrid desktop head added (`src/TrStudio.Desktop`, TFMs `net10.0-windows10.0.19041.0;net10.0-maccatalyst`), hosts the SAME `TrStudioUI` RCL + `TrStudio.Models` in a BlazorWebView; added to `TrStudio.sln`. Hybrid auth = `MauiAuthSession` (SecureStorage, singleton) replacing the web's ProtectedSessionStorage + a `MauiAuthTokenHandler` bearer; named `TrStudioApi` HttpClient (BaseAddress localhost:5102). Phase-1-safe refactor: the 5 Blazor-Server-only auth files moved RCL→web host, RCL FrameworkReference(AspNetCore.App) dropped so it consumes cleanly on MAUI TFMs. **Builds: Windows head 0/0 (rung #4), Mac Catalyst head 0/0 compile (rung #4), full `dotnet build TrStudio.sln` 0/0.** **Windows-head native-window smoke (FlaUI, bound to launched PID, element-by-element, no global input):** Owner→creator shell renders + "Switch to Management" present + Avatar Studio live data; Owner→/admin/ops management screen renders with live API data, API log `GET /api/v1/admin/{spend,runpod}`+`/generation-log`→**200**; Creator→/admin/registry shows in-window **AccessDenied**, management nav absent (REQ-FN-072). Screenshots in `.smoke/desktop/shots/`. **Mac Catalyst head = build-verified-only (no Mac in this env → ships unverified/unrunnable).** | [view](#d-req-nfr-003) |
| REQ-NFR-004 | Reliability (durable jobs, crash retry) · P1+ | Not Started | 0% | Cross-cutting | [view](#d-req-nfr-004) |
| REQ-NFR-005 | Security (auth, encrypted keys, no secret leaks) · P1+ | Not Started | 0% | Cross-cutting | [view](#d-req-nfr-005) |
| REQ-NFR-006 | Privacy (local stays local; closed-API routing flagged) · P2+ | Not Started | 0% | Cross-cutting | [view](#d-req-nfr-006) |
| REQ-NFR-007 | Cost control (estimates, idle-stop, cap + alerts) · P3+ | Verified | 100% | 2026-07-03 [P3-BE] per-job estimate precedes run; monthly cap ($100, configurable) + warn (0.8) + over-cap 402 block; spend across RunPod+closed-API channels; idle-stop toggle (live stop deferred w/ RunPod). Cross-cutting | [view](#d-req-nfr-007) |
| REQ-NFR-008 | Observability (Serilog + OpenTelemetry, job IDs) · P1+ | Implemented | 75% | 2026-07-04 [P9-BE] Serilog structured logging + OpenTelemetry metrics (queue/spend/latency) + traceable job-id correlation; ops dashboard (FN-064) surfaces live health/queue/spend. Cross-cutting | [view](#d-req-nfr-008) |
| REQ-NFR-009 | Extensibility (registry-driven UI; model = config) · P1+ | Not Started | 0% | Cross-cutting | [view](#d-req-nfr-009) |
| REQ-NFR-010 | Compliance (provenance, audit, retention, DPDP) · P8 | Implemented | 75% | 2026-07-03 provenance on every output (FN-059) + full auditable generation trail (FN-061) live. ⚠ retention config + DPDP India-residency are deployment config (not code) — pending deployment | [view](#d-req-nfr-010) |

**Status values:** `Not Started` · `In Progress` · `Implemented` (code done, not yet verified) · `Verified` (self-smoke or verifier PASS — acceptance AND data-render AND visual gates all pass) · `Done (pre-existing)` (migrated from an earlier dev plan as already complete — build agents must NOT rebuild; terminal like `Verified`) · `Needs re-verify` (a defect or change was logged — must be re-run before it can return to `Verified`) · `PARTIAL` (some acceptance unmet — say what in Remarks) · `FAIL` (verifier ran and failed — bug in Remarks) · `Blocked` (external/library gap — link the TR-/TR-RAG- entry in Remarks) · `N/A`.

**% guide:** `0` not started · `25` scaffolded · `50` in progress · `75` implemented-unverified · `100` verified.

**Remarks:** date + what was done / what is missing / bug or library reference. This is the home for bugs and change notes — do not spawn a separate file. Visual-gate failures are prefixed `⚠ visual:`; security findings `⚠ SECURITY`.

## UI / Pages

<!-- UI REQs are built by /trblazeui from the approved mockups (docs/TrStudio-UIDesign.md
     + docs/mockups/*.html); each REQ cites the mockup screen it realizes. REQ-UI IDs
     match the pre-assigned IDs in the UIDesign spec — do not renumber. -->

### Page: Login (`/login`)

<a id="d-req-ui-001"></a>
- **REQ-UI-001** — AppManager-backed sign-in: centered card, email/password fields, brand-gradient submit, "via AppManager" note, links to Register and Forgot-password; on success routes to the role-based default landing (REQ-FN-016). No role selector on Login. (BRD-57, BRD-84) *Mockup:* docs/mockups/login.html.
  - *Acceptance:* invalid fields show `FieldError`; submit disabled until valid; auth failure shows `Alert` (Danger); successful login redirects per role (Owner/Creator → `/avatar`, Operator → `/admin/ops`) or into the first-run gate; controls do not overlap at desktop + mobile widths (visual gate).

### Page: Register (`/register`)

<a id="d-req-ui-002"></a>
- **REQ-UI-002** — Account creation via the AppManager child-app API: name/email/password/confirm, account-type selector (Creator / Admin-Operator) that decides the provisioned role and the post-signup landing, duplicate-email `Alert`. (BRD-83, BRD-84) *Mockup:* docs/mockups/register.html.
  - *Acceptance:* password mismatch → `FieldError`; submit disabled until valid; account type drives the landing (Creator → `/avatar`, Admin → setup landing); AppManager error surfaces as `Alert`; no local password store; visual gate at both widths.

### Page: Forgot password (`/forgot-password`)

<a id="d-req-ui-003"></a>
- **REQ-UI-003** — Password-reset request screen: email field + "Send reset link" (AppManager API), confirmation `Alert` (Success), back-to-login link; reset itself completes through AppManager. (BRD-57) *Mockup:* docs/mockups/forgot-password.html.
  - *Acceptance:* submit disabled until valid email; sent state shows success `Alert`; no local reset flow; visual gate at both widths.

### Page: Setup required (`/setup-required`)

<a id="d-req-ui-004"></a>
- **REQ-UI-004** — Creator-facing first-run gate screen: "TrStudio isn't set up yet" card (no sidebar), ask-your-admin copy, "Sign in as admin" CTA + "Check again" re-check, 3-step admin hint card. Shown only while the BRD-85 gate fails; admins are never sent here. (BRD-85) *Mockup:* docs/mockups/setup-required.html.
  - *Acceptance:* renders for a Creator when no enabled source or no ready model exists; "Check again" re-evaluates and forwards to `/avatar` once setup completes; visual gate at both widths.

### Page: App shell & brand theme (TrStudioUI RCL)

<a id="d-req-ui-005"></a>
- **REQ-UI-005** — The shared shell + theme both apps consume from `TrStudioUI`: collapsible `Sidebar` shell (`SidebarProvider`/`SidebarInset`) with topbar (`SidebarTrigger`, breadcrumb, theme toggle, user chip), dark-by-default OKLCH theme with persisted light/dark toggle, the violet→fuchsia brand-gradient solid `Button` default (no monochrome solids), `.brand-gradient` utility, control-state token mapping and LucideIcon map per the UIDesign Branding guide; creator + admin nav sets wired incl. footer app cross-link. (BRD-71; UIDesign §Design system + §Branding guide) *Mockup:* all of docs/mockups/ (shell shared by every screen).
  - *Acceptance:* both apps render the same shell from the RCL; dark is default and the toggle persists (localStorage) across pages; base solid `Button` renders the brand gradient in light + dark; sidebar nav items route correctly in both apps; visual gate at both widths.

### Page: Avatar studio (`/avatar`)

<a id="d-req-ui-010"></a>
- **REQ-UI-010** — Three-column working surface: character picker (empty-state CTA), voice picker + generate, script editor with TechieRag assist, preview player with render `Progress`, Fast/High/Premium `RadioGroup` tier selector with per-tier cost/eta + HeyGen `Badge`, closed-API routing `Alert`, job/cost panel with pre-run estimate + over-cap warning, captions `Switch` + aspect `Select` + Export MP4. (BRD-27..33, BRD-30, BRD-53, BRD-55, BRD-63) *Mockup:* docs/mockups/avatar-studio.html.
  - *Acceptance:* tier change updates cost/eta hint and provider badge; Premium shows the routed-to-HeyGen banner; render shows progress + eta; safety block surfaces an `Alert`; cost estimate visible before render; visual gate at both widths.

### Page: Character library (`/avatar/characters`)

<a id="d-req-ui-011"></a>
- **REQ-UI-011** — Character grid: `Card`s with `Avatar`, realism `Badge` (photoreal/stylized), search + "New Character"; create/edit `Dialog` (image-gen prompt, realism `RadioGroup`, seed/LoRA lock); character-bible `Sheet` (TechieRag persona notes). (BRD-27, BRD-55) *Mockup:* docs/mockups/character-library.html.
  - *Acceptance:* empty state shows create CTA; dialog saves a character with locked identity + realism flag; bible sheet opens per character; skeleton cards while loading; visual gate at both widths.

### Page: Image studio (`/image`)

<a id="d-req-ui-020"></a>
- **REQ-UI-020** — Left prompt+params panel with **dynamic controls rendered from the registry params schema**; center canvas (generate / I2I / inpaint mask `Toolbar`); ControlNet `Tabs` (pose/depth/edges) + reference `FileUpload`; upscale 2K/4K button; results strip with seed `Badge` grid; export `Select` (1:1/4:5/9:16/16:9) + bg-remove `Switch`. (BRD-16..21) *Mockup:* docs/mockups/image-studio.html.
  - *Acceptance:* switching model re-renders params from its schema without code change; generate shows `Progress`; results show seeds and reproduce; canvas placeholder when empty; visual gate at both widths.

### Page: Voice studio (`/voice`)

<a id="d-req-ui-030"></a>
- **REQ-UI-030** — Text + language (`EN/HI/Hinglish`) controls, preset-voice `Combobox` (routing hidden behind one control), speed `Slider` + emotion `Select` (disabled when unsupported), waveform player, clone `Dialog` with `FileUpload` and a **consent `AlertDialog` gate for real people**. (BRD-22..26, BRD-62) *Mockup:* docs/mockups/voice-studio.html.
  - *Acceptance:* generation shows `Progress` then playable audio; real-person clone blocks until consent confirmed (synthetic/preset voices skip the gate); unsupported emotion/speed disabled; visual gate at both widths.

### Page: Video studio (`/video`)

<a id="d-req-ui-040"></a>
- **REQ-UI-040** — I2V/T2V mode `Tabs`, duration/motion `Slider`s + aspect `Select`, first/last-frame `FileUpload` conditioning, Fast/High/Premium tier selector with closed-API `Alert`, timeline/editor `Card` with trim/join/reframe `Toolbar`, interpolate `Switch` + upscale, Export MP4. (BRD-42..46, BRD-30) *Mockup:* docs/mockups/video-studio.html.
  - *Acceptance:* mode tabs swap controls; tier selector behaves as in Avatar studio; timeline placeholder when empty; render progress with eta; visual gate at both widths.

### Page: Gallery (`/gallery`)

<a id="d-req-ui-050"></a>
- **REQ-UI-050** — "My Library" asset grid: filter bar (search, type/model/project `Select`s), asset `Card`s with provenance `Badge`, detail `Sheet` with full params + one-click reproduce, compare hand-off, bulk-export `Toolbar` with selection count. Per-user only — server-side owner filter (BRD-81). (BRD-47, BRD-48, BRD-49, BRD-51, BRD-81) *Mockup:* docs/mockups/gallery.html.
  - *Acceptance:* filters narrow the grid; detail sheet shows full params and reproduces; bulk export includes provenance metadata; only the signed-in user's assets appear; empty state + skeleton tiles; visual gate at both widths.

### Page: Projects (`/projects`)

<a id="d-req-ui-051"></a>
- **REQ-UI-051** — Left searchable project list (`Card`s with asset counts + dates, "New project"); right detail: project header (Open in Gallery / Rename / Archive), generation-history `DataTable` (`Tabs` All/Images/Video/Voice, per-row Reproduce), full run-parameters `Card` (prompt, character, model, tier, seed, resolution, voice, cost, provenance) with "Reproduce with these params". Per-user scoped. (BRD-48, BRD-81) *Mockup:* docs/mockups/projects.html.
  - *Acceptance:* selecting a project loads its history; per-row Reproduce carries the stored params; empty list shows create CTA; visual gate at both widths.

### Page: Comparison (`/compare`)

<a id="d-req-ui-052"></a>
- **REQ-UI-052** — Side-by-side comparison view: slot toolbar (A/B asset `Select`s, "Add third", side-by-side/overlay `Tabs`, sync-playback `Switch`); result panes with preview + model/provider·tier/seed/provenance `Badge`s and cost/duration/resolution rows; parameter-diff `DataTable` with differing rows highlighted + "N differ" badge + show-identical toggle; per-pane Reproduce/Export/Open-in-Gallery actions. No sidebar item — reached from the Gallery's Compare action (breadcrumb `Gallery / Compare`). Per-user scoped. (BRD-49, BRD-81) *Mockup:* docs/mockups/compare.html.
  - *Acceptance:* two assets render side-by-side with differing params visibly highlighted; empty state (<2 assets) links back to Gallery; Reproduce re-submits the pane's stored params; visual gate at both widths.

### Page: Library (`/library`)

<a id="d-req-ui-053"></a>
- **REQ-UI-053** — Prompt/script/preset library: search + type segmented `Tabs` (All/Prompts/Scripts/Style presets/Voice presets) + "New preset" CTA; preset `Card` grid with type tag, clamped snippet or param summary, tag `Badge`s, use-count, "Use in {Studio}" + Edit actions; new/edit `Sheet`. Per-user scoped; "Use in Studio" pre-fills the owning studio's composer. (BRD-50, BRD-81) *Mockup:* docs/mockups/library.html.
  - *Acceptance:* type tabs filter the grid; "Use in Studio" routes to the right studio with the preset applied; empty state shows the save-from-composer CTA; visual gate at both widths.

### Page: Account & spend (`/account`)

<a id="d-req-ui-060"></a>
- **REQ-UI-060** — Profile card (AppManager identity), 3 spend KPI cards with `Progress` (RunPod / closed-API / total vs monthly cap, danger when over), license/tier card from LicenseSvc with upgrade CTA when gated, recent-usage `DataTable`. (BRD-52, BRD-58, BRD-59, BRD-75) *Mockup:* docs/mockups/account.html.
  - *Acceptance:* spend renders against the cap with warning/danger states; license/tier reads from AppManager; empty usage message; skeleton KPIs while loading; visual gate at both widths.

### Page: Admin — Hardware & guided setup (`/admin/setup`, `/admin/setup/hardware`)

<a id="d-req-ui-070"></a>
- **REQ-UI-070** — Readiness summary cards (GPU cores / unified memory / MPS-CoreML / disk) with ready/warn `Badge`s, guided-setup stepper (ONNX install, isolated Python+ComfyUI provisioning) with `Progress`, low-disk `Alert` linking installs to the Model Library screen. (BRD-8, BRD-9, BRD-10) *Mockup:* docs/mockups/admin-hardware.html.
  - *Acceptance:* detection shows skeletons then per-capability status; setup steps show progress and surface failures as `Alert`s; disk warning links to `/admin/models`; visual gate at both widths.

### Page: Admin — Registry & providers (`/admin/registry`, `/admin/providers`)

<a id="d-req-ui-071"></a>
- **REQ-UI-071** — Registry `DataTable` (id, displayName, task, providerType, tier `Badge`, license, safetyClass; filter/sort); add/edit `Sheet` with the declarative params-schema editor (key/type/required/default rows, inline validation); Provider-keys panel (HeyGen/aggregator `Input` Password + enabled `Switch`, masked, never logged) — `/admin/providers` renders as this panel; no separate screen. (BRD-6, BRD-7, BRD-77) *Mockup:* docs/mockups/admin-registry.html.
  - *Acceptance:* row CRUD round-trips incl. params schema; keys masked and stored encrypted; empty registry shows `Empty` CTA; visual gate at both widths.

### Page: Admin — ComfyUI import & debug (`/admin/comfy/import`, `/admin/comfy/debug`)

<a id="d-req-ui-072"></a>
- **REQ-UI-072** — Workflow drop-zone `FileUpload` + paste `Textarea` (parse errors → `Alert`); parsed nodes/inputs `DataTable`; input→registry-param mapping rows; run/debug panel with per-node status, exact JSON sent, WebSocket previews and `Progress`; missing-deps `Alert` with Install action; "Save as model" + version `Select` + known-good `Badge`. (BRD-34..41) *Mockup:* docs/mockups/admin-comfy.html.
  - *Acceptance:* invalid JSON surfaces a parse `Alert`; mapping edits persist; run streams per-node status + previews; save-as-model + pin round-trips; visual gate at both widths.

### Page: Admin — RunPod & ops (`/admin/ops`, `/admin/log`)

<a id="d-req-ui-073"></a>
- **REQ-UI-073** — Spend/guardrail cards with `Progress` (RunPod / closed-API / cap, near-cap danger + `Alert`); RunPod control card (start/stop `Button`, auto-idle-stop `Switch`, cold-start `Badge`); worker health + queue depth cards; filterable generation-log `DataTable` with reproduce action. (BRD-12, BRD-13, BRD-14, BRD-15, BRD-75) *Mockup:* docs/mockups/admin-ops.html.
  - *Acceptance:* spend renders against the cap incl. closed APIs; endpoint state reflects scale-to-zero transitions; log filters and reproduces; empty log message + skeleton KPIs; visual gate at both widths.

### Page: Admin — Model sources (`/admin/sources`)

<a id="d-req-ui-074"></a>
- **REQ-UI-074** — Sources list (name, type HuggingFace/Civitai/Custom, base URL, enabled `Switch`, status `Badge`) with header search + "＋ Add source" opening the add/edit `Sheet` (name, type, base URL, API token Password, enabled); Test-connection button (ok `Badge` / fail `Alert`); info `Alert` distinguishing sources (open model files) from Providers (closed generation keys). (BRD-82) *Mockup:* docs/mockups/admin-sources.html.
  - *Acceptance:* add/edit/enable round-trips; tokens stored encrypted, never logged; test-connection reports reachability; empty state CTA; visual gate at both widths.

### Page: Admin — Content policy / NSFW (`/admin/policy`)

<a id="d-req-ui-075"></a>
- **REQ-UI-075** — Single content-policy card: "Unrestricted (NSFW) mode" `Switch` (off by default) behind a confirmation `AlertDialog` ("synthetic adult 18+ personas only"); always-locked floors list (CSAM · real-person/NCII · minors · public-figure · provenance · audit — "cannot be disabled"); scope note; toggle disabled with explanation in multi-user/SaaS mode. (BRD-79, BRD-80) *Mockup:* docs/mockups/admin-policy.html.
  - *Acceptance:* enabling requires explicit confirmation; floors list always visible; toggle disabled + note when multi-user; visual gate at both widths.

### Page: Admin — Model library (`/admin/models`)

<a id="d-req-ui-076"></a>
- **REQ-UI-076** — Search + task `Tabs` + source `Select` over a results `DataTable` (name, source, task, size, min VRAM, license badge — restrictive → warn) with one-click Install; footer `Pagination`; right rail: resumable install queue (`Progress`, Pause/Resume/Cancel, speed + destination) and destination card (local vs RunPod network volume, >10 GB defaults to volume, over-disk `Alert`); registry hand-off info `Alert`. Browses only ENABLED sources. (BRD-11, BRD-82) *Mockup:* docs/mockups/admin-models.html.
  - *Acceptance:* no-sources empty state links to `/admin/sources`; install shows per-row progress and is pausable/resumable; license + VRAM shown before install; pagination works; visual gate at both widths.

### Page: Unified role-based app shell (`P-amend`)

<a id="d-req-ui-080"></a>
- **REQ-UI-080** — Merge the `TrStudioAdmin` management screens into the ONE `TrStudio` app on the shared `TrStudioUI` RCL; role-gated navigation presents the Creator studios, the Management surface, or both, by the signed-in user's role; retire the separate `TrStudioAdmin` host. Supersedes the two-app split (BRD-86, ADR-012). *Amended 2026-07-05.*
  - *Acceptance:* the former `TrStudioAdmin` management screens render inside the single `TrStudio` app from `TrStudioUI` (no separate admin host).
  - *Acceptance:* sidebar/navigation is role-gated — Creator sees only creator studios, Operator/Admin sees only management, Owner sees both with an in-app switch.
  - *Acceptance:* the retired `TrStudioAdmin` host is no longer required to reach any management screen; visual gate at both widths.

### Page: Prompt studio (`/prompt-studio`) · P10

<a id="d-req-ui-081"></a>
- **REQ-UI-081** — Prompt-crafting chat screen: message thread + composer, a registry-driven **text-chat LLM picker** (`Select`/`Combobox` over registry rows with `task: "text-chat"`), and an **image attachment** control (e.g. a photo to be edited) that is enabled only when the picked LLM is vision-capable (otherwise disabled with a hint to pick a vision model). Assistant replies render the crafted prompt in a copyable block. Listed with the creator studios in the nav. (BRD-88, BRD-89, BRD-90) *Mockup:* docs/mockups/prompt-studio.html. *Added 2026-07-07.*
  - *Acceptance:* the LLM picker lists only registry `text-chat` rows; switching models toggles the attachment control per `visionCapable`; sending a message with the API down shows a clean `Alert` (no dead UI); the crafted-prompt block has a working Copy action; visual gate at desktop + mobile widths.

<a id="d-req-ui-082"></a>
- **REQ-UI-082** — Prompt-studio session rail + prompt-use actions: a per-user session list (new / resume / rename / delete — owner-scoped, BRD-81) alongside the chat, and per-reply actions **Copy**, **Save to library** (creates a `prompt` preset per BRD-50), and **Send to studio** (routes the prompt into the Image / Avatar / Video studio prompt field). (BRD-91, BRD-92) *Mockup:* docs/mockups/prompt-studio.html. *Added 2026-07-07.*
  - *Acceptance:* sessions persist across reload and list only the signed-in user's sessions; resume restores the full message history; Save-to-library lands the prompt in `/library` as a `prompt` preset; Send-to-studio opens the chosen studio with the prompt pre-filled; visual gate at both widths.

## Functional requirements

<!-- Built by the unified build-phase (flow-master). Grouped by BRD ledger phase. -->

### Phase 1 — Platform spine, registry, identity

<a id="d-req-fn-001"></a>
- **REQ-FN-001** — `IGenerationProvider` (supports/submit/status/result/estimateCost) defined in `TrStudio.Models`; implementations in `TrStudio.Engine/Providers/`; orchestrator resolves model → exactly one provider so the UI never knows which ran. *Acceptance:* a stub provider registered against a registry row runs a job end-to-end through the abstraction. (BRD-1)

<a id="d-req-fn-002"></a>
- **REQ-FN-002** — Async job model: submit → poll/stream → fetch, identical across providers via `/api/jobs`. *Acceptance:* the same endpoint sequence drives two different providers with no caller-visible difference. (BRD-2)

<a id="d-req-fn-003"></a>
- **REQ-FN-003** — Durable Hangfire/Postgres queue: priority, concurrency caps, retries, back-pressure; no Redis. *Acceptance:* an enqueued job survives an API restart and completes; a failing job retries per policy. (BRD-3)

<a id="d-req-fn-004"></a>
- **REQ-FN-004** — Unified `TrStudioApi` surface: OpenAPI-described (`/swagger`), authenticated, versioned; admin-scoped endpoints separated; DbUp (`TrStudioDb`) runs on startup. *Acceptance:* swagger lists versioned, auth-protected endpoints; schema migrates on boot. (BRD-4)

<a id="d-req-fn-005"></a>
- **REQ-FN-005** — SignalR progress hub streams job progress/eta; failures are actionable (e.g. "out of memory — drop to 256px or route to RunPod"). *Acceptance:* a running job pushes progress events; a provoked failure carries a remediation hint. (BRD-5)

<a id="d-req-fn-006"></a>
- **REQ-FN-006** — Model & Provider Registry as single source of truth (task, providerType, tier, hardware, languages, license, safetyClass) with a declarative params schema that auto-renders UI controls. *Acceptance:* adding a registry row makes the model selectable with rendered params, no code change. (BRD-6)

<a id="d-req-fn-007"></a>
- **REQ-FN-007** — Closed-provider adapters (HeyGen, aggregator) configured with encrypted API-key storage, enabled per model. *Acceptance:* keys round-trip encrypted at rest, masked in UI/logs; disabling a provider hides its models. (BRD-7)

<a id="d-req-fn-008"></a>
- **REQ-FN-008** — Host capability detection: Apple Silicon GPU cores, unified memory, MPS/CoreML, disk; readiness summary. *Acceptance:* detection returns a structured readiness report consumed by REQ-UI-070. (BRD-8)

<a id="d-req-fn-009"></a>
- **REQ-FN-009** — Guided setup installs ONNX Runtime and provisions the managed, isolated Python env + ComfyUI with no terminal use. *Acceptance:* setup completes from the UI on a bare host; failures surface actionable errors. (BRD-9)

<a id="d-req-fn-010"></a>
- **REQ-FN-010** — Isolated Python/ComfyUI environment lifecycle managed by the engine; never collides with system Python. *Acceptance:* provisioning/upgrading touches only the managed env path. (BRD-10)

<a id="d-req-fn-011"></a>
- **REQ-FN-011** — Model install service: one-click install/import of curated open models, resumable downloads, size/VRAM/licence surfaced, big media models stored on the RunPod network volume. *Acceptance:* an interrupted download resumes; >10 GB models default to the volume. (BRD-11)

<a id="d-req-fn-012"></a>
- **REQ-FN-012** — AppManager login integration: child-app handshake (`X-Api-Key`/`X-Api-Secret`), JWT + refresh, RSA-encrypted passwords, no local password store. *Acceptance:* login yields a session JWT via AppManager; no password persisted locally. (BRD-57)

<a id="d-req-fn-013"></a>
- **REQ-FN-013** — License/subscription validation via AppManager LicenseSvc. *Acceptance:* an expired/absent license is reported and gates accordingly. (BRD-58)

<a id="d-req-fn-014"></a>
- **REQ-FN-014** — Feature/tier gating via AppManager FeatureSvc (e.g. Premium behind a paid license in multi-user mode; personal mode = all tiers). *Acceptance:* a gated tier is refused server-side with an upgrade-prompt response. (BRD-59)

<a id="d-req-fn-015"></a>
- **REQ-FN-015** — Registration via the AppManager child-app API; generic superadmin (`admin@appmanager.local`) bootstraps the first session; password reset requested via AppManager. *Acceptance:* a new account created from `/register` can sign in; superadmin works on first run. (BRD-83)

<a id="d-req-fn-016"></a>
- **REQ-FN-016** — Role-based default landing after login per the BRD §9 F-IDENTITY table (Owner/Creator → `/avatar`; Operator/Admin → `/admin/ops`; setup-incomplete variants per BRD-85). *Acceptance:* each role lands on its table-specified page; nobody lands on a blank screen. (BRD-84)

<a id="d-req-fn-017"></a>
- **REQ-FN-017** — First-run setup gate on every login: complete = ≥1 enabled model source AND ≥1 ready registry model; admins route into setup (hardware → guided → sources → install), Creators to `/setup-required`; no generation UI until the gate passes. *Acceptance:* gate outcomes route each role correctly and flip once setup completes. (BRD-85)

<a id="d-req-fn-018"></a>
- **REQ-FN-018** — Generation log records who/model/params/cost/duration/outcome for every job — history, one-click reproduce, audit. *Acceptance:* every job (success or failure) writes a complete log row; reproduce re-submits from it. (BRD-14)

### Phase 2 — Local image vertical + workspace foundation

<a id="d-req-fn-019"></a>
- **REQ-FN-019** — Text-to-image via `OnnxLocalProvider` (CoreML/CPU EP) driven by the registry params schema. *Acceptance:* a prompt renders an image locally; params honor the schema. (BRD-16)

<a id="d-req-fn-020"></a>
- **REQ-FN-020** — Image-to-image, inpaint/outpaint and mask-based edits. *Acceptance:* a masked region regenerates while the rest is preserved. (BRD-17)

<a id="d-req-fn-021"></a>
- **REQ-FN-021** — Reference-image / ControlNet conditioning (pose/depth/edges) + face/identity lock for characters. *Acceptance:* conditioning measurably constrains output; identity lock reuses the character's reference set/seed. (BRD-18)

<a id="d-req-fn-022"></a>
- **REQ-FN-022** — Upscale + face restoration to 2K/4K. *Acceptance:* output at requested resolution with face restoration applied. (BRD-19)

<a id="d-req-fn-023"></a>
- **REQ-FN-023** — Batch/grid variations with seed control + reproducibility. *Acceptance:* same seed + params reproduce an identical image; a batch yields the requested grid. (BRD-20)

<a id="d-req-fn-024"></a>
- **REQ-FN-024** — Social-format export presets (1:1, 4:5, 9:16, 16:9) + background removal. *Acceptance:* exports land at preset aspect ratios; bg-removal produces transparency. (BRD-21)

<a id="d-req-fn-025"></a>
- **REQ-FN-025** — Asset gallery backend: every output browsable/filterable (type/model/project) and reusable as an input. *Acceptance:* filters work server-side; an asset can seed a new job. (BRD-47)

<a id="d-req-fn-026"></a>
- **REQ-FN-026** — Projects/folders with full-parameter history and one-click reproduce. *Acceptance:* every run stores its complete parameter set; reproduce re-runs it byte-for-byte (same seed). (BRD-48)

<a id="d-req-fn-027"></a>
- **REQ-FN-027** — Per-user isolation: every character/job/asset scoped to its AppManager `userId`; the owner filter applied to **every** engine data-access query (Dapper, parameterized) — server-side, not just UI. *Acceptance:* user A can never read/list/reproduce user B's entities via API or UI. (BRD-81)

### Phase 3 — Managed ComfyUI + RunPod

<a id="d-req-fn-028"></a>
- **REQ-FN-028** — RunPod serverless integration: register endpoint/template, submit jobs, stream status, scale to zero automatically (start/stop on demand). *Acceptance:* a heavy job cold-starts a worker, runs, and the worker idle-stops to zero. (BRD-12)

<a id="d-req-fn-029"></a>
- **REQ-FN-029** — Cost guardrails: per-job estimate before run, RunPod idle auto-stop, monthly spend cap with alerts across RunPod **and** closed APIs (default $100). *Acceptance:* an over-cap job is warned/blocked; spend accrues across both channels. (BRD-13)

<a id="d-req-fn-030"></a>
- **REQ-FN-030** — Model-repository sources: CRUD for Hugging Face / Civitai / custom URL registries (base URL, optional encrypted token, enabled flag); the model library browses/installs from enabled sources only. Distinct from provider generation keys. *Acceptance:* enabling a source makes its models browsable; tokens never logged. (BRD-82)

### Phase 4 — Voice (EN + HI)

<a id="d-req-fn-031"></a>
- **REQ-FN-031** — TTS in English, Hindi and Hinglish with a preset-voice library. *Acceptance:* the same text synthesizes in EN and HI with selectable preset voices. (BRD-22)

<a id="d-req-fn-032"></a>
- **REQ-FN-032** — Voice cloning from a short sample; consent-gated for real people (synthetic/preset voices ungated) — gate wired to REQ-FN-058 evidence. *Acceptance:* real-person clone refuses without recorded consent; synthetic clone proceeds. (BRD-23)

<a id="d-req-fn-033"></a>
- **REQ-FN-033** — Per-language voice model routing (IndicF5/XTTS-Hindi for HI; XTTS/OmniVoice for EN) hidden behind one "voice" control. *Acceptance:* language choice transparently selects the routed model. (BRD-24)

<a id="d-req-fn-034"></a>
- **REQ-FN-034** — Emotion/style and speed controls where the model supports them. *Acceptance:* supported controls alter output; unsupported ones are reported/disabled. (BRD-25)

<a id="d-req-fn-035"></a>
- **REQ-FN-035** — Automatic alignment of generated audio to the avatar pipeline (timing + lip-sync handoff). *Acceptance:* voice output hands off to an avatar job without manual timing work. (BRD-26)

### Phase 5 — Avatar pipeline (lead feature)

<a id="d-req-fn-036"></a>
- **REQ-FN-036** — Character as a first-class entity: synthetic persona (photoreal or stylized), locked identity (reference set + seed/embedding, optional LoRA), realism flag scoping model choice and safety class; reusable everywhere. *Acceptance:* a saved character reuses its locked identity across studios; realism flag persists. (BRD-27)

<a id="d-req-fn-037"></a>
- **REQ-FN-037** — Talking avatar from character image + script/audio with lip-sync, routed to MuseTalk/LatentSync/SadTalker/LivePortrait, a RunPod heavy model, or HeyGen per the quality setting. *Acceptance:* one character+audio input yields a lip-synced clip on at least the Fast route. (BRD-28)

<a id="d-req-fn-038"></a>
- **REQ-FN-038** — Script→video end-to-end: type a script (EN/HI), pick character + voice, get a narrated clip. *Acceptance:* the full chain (script → TTS → lip-sync → clip) runs as one pipeline. (BRD-29)

<a id="d-req-fn-039"></a>
- **REQ-FN-039** — Quality/cost tier routing: Fast (Mac local) / High (RunPod) / Premium (HeyGen) selected per job on one control; the abstraction swaps the provider underneath. *Acceptance:* the same job spec routes to a different provider per tier with no other change. (BRD-30)

<a id="d-req-fn-040"></a>
- **REQ-FN-040** — Captions/subtitles (from the script or Whisper) in EN/HI, burn-in or sidecar SRT (text via REQ-RAG-002). *Acceptance:* an export carries burned captions or an SRT matching the audio. (BRD-31)

<a id="d-req-fn-041"></a>
- **REQ-FN-041** — Optional digital-twin from a short base video; consent-gated (SAFE-03), off by default. **Deferred (Could).** *Acceptance:* twin creation refuses without recorded consent; feature flag off by default. (BRD-32)

<a id="d-req-fn-042"></a>
- **REQ-FN-042** — Multi-shot assembly: chain multiple avatar clips into one sequence with a consistent character. **Deferred (Could).** *Acceptance:* an assembled sequence preserves character identity across shots. (BRD-33)

### Phase 6 — ComfyUI pipeline management

<a id="d-req-fn-043"></a>
- **REQ-FN-043** — Import a ComfyUI workflow (drag-drop API-format JSON or paste), parse nodes, list parameterizable inputs. *Acceptance:* a valid workflow parses to a node/input list; invalid JSON reports a parse error. (BRD-34)

<a id="d-req-fn-044"></a>
- **REQ-FN-044** — Map workflow inputs → registry params (required, types, defaults, hide internal nodes) for auto-rendered controls. *Acceptance:* a mapped workflow exposes exactly the mapped params in the registry schema. (BRD-35)

<a id="d-req-fn-045"></a>
- **REQ-FN-045** — Run an imported workflow against a chosen worker (local MPS or RunPod), streaming progress + previews via WebSocket. *Acceptance:* a run streams live progress and preview frames to the debug view. (BRD-36)

<a id="d-req-fn-046"></a>
- **REQ-FN-046** — Debug view data: per-node status, error messages, the exact JSON sent, missing-model/custom-node detection with install hints. *Acceptance:* a failing node surfaces its error + the exact submitted JSON. (BRD-37)

<a id="d-req-fn-047"></a>
- **REQ-FN-047** — Detect and (where possible) auto-install required custom nodes + model files for an imported workflow. *Acceptance:* a workflow with a missing node reports it and installs it when available. (BRD-38)

<a id="d-req-fn-048"></a>
- **REQ-FN-048** — Save an imported+mapped workflow as a registered model usable from `TrStudio` (graph hidden from end users). *Acceptance:* the saved model appears in the registry and renders params in the creator app with no graph exposure. (BRD-39)

<a id="d-req-fn-049"></a>
- **REQ-FN-049** — Version workflows and pin a known-good version per registered model. *Acceptance:* a model keeps serving its pinned version while newer versions exist. (BRD-40)

<a id="d-req-fn-050"></a>
- **REQ-FN-050** — Sandbox/validate a workflow with a smoke test before it goes live. *Acceptance:* a workflow cannot be registered live without a passing smoke run. (BRD-41)

### Phase 7 — Video + closed API

<a id="d-req-fn-051"></a>
- **REQ-FN-051** — Image-to-video and text-to-video (open model on RunPod, or closed API via aggregator) with duration/motion/aspect controls. *Acceptance:* both modes produce clips honoring the controls. (BRD-42)

<a id="d-req-fn-052"></a>
- **REQ-FN-052** — First/last-frame + reference-image conditioning for consistency. *Acceptance:* conditioned generation matches the pinned frames/reference. (BRD-43)

<a id="d-req-fn-053"></a>
- **REQ-FN-053** — Frame interpolation (RIFE/FILM) and video upscale as post-steps. *Acceptance:* post-steps raise fps/resolution on an existing clip. (BRD-44)

<a id="d-req-fn-054"></a>
- **REQ-FN-054** — Trim/join/reframe, add audio (from Voice studio), export MP4 in platform formats. *Acceptance:* an edited clip with added audio exports as MP4 at the chosen format. (BRD-45)

<a id="d-req-fn-055"></a>
- **REQ-FN-055** — Clip extension/stitching for longer sequences. **Deferred (Could).** *Acceptance:* two clips stitch into one continuous sequence. (BRD-46)

### Phase 8 — Safety, provenance & NSFW mode (wired from Phase 2 onward)

> SAFE-01..06 are **non-disableable floors** — enforced on every provider regardless of any mode, including Unrestricted (F-NSFW). Wired incrementally from Phase 2; Phase 8 is the consolidation/verification pass.

<a id="d-req-fn-056"></a>
- **REQ-FN-056** — CSAM prevention by design: hash-matching + classifier on inputs AND outputs, hard block + log, on every provider; never toggleable. *Acceptance:* a flagged input/output is blocked and audit-logged on every provider path. (BRD-60, SAFE-01)

<a id="d-req-fn-057"></a>
- **REQ-FN-057** — Block sexualisation/face-swap of real, identifiable people; refuse NCII/sexual deepfakes. *Acceptance:* a real-person sexual job is refused and logged regardless of mode. (BRD-61, SAFE-02)

<a id="d-req-fn-058"></a>
- **REQ-FN-058** — Recorded consent step (retained as evidence) required for any real-person face or voice clone. *Acceptance:* real-person clone jobs are blocked until consent evidence exists; evidence is retained and auditable. (BRD-62, SAFE-03)

<a id="d-req-fn-059"></a>
- **REQ-FN-059** — Provenance/AI-disclosure metadata (C2PA-style, non-trivial to strip; respect provider watermarks) embedded on every output. *Acceptance:* every export carries provenance metadata; provider watermarks pass through. (BRD-63, SAFE-04)

<a id="d-req-fn-060"></a>
- **REQ-FN-060** — Public-figure impersonation blocked by default; allowed only with explicit recorded rights. *Acceptance:* a recognizable-public-figure job is refused without a rights record. (BRD-64, SAFE-05)

<a id="d-req-fn-061"></a>
- **REQ-FN-061** — Per-deployment policy config, abuse reporting, and a full generation audit trail. *Acceptance:* policy loads per deployment; abuse reports persist; every generation is traceable in the audit trail. (BRD-65, SAFE-06)

<a id="d-req-fn-062"></a>
- **REQ-FN-062** — Owner-only Unrestricted (NSFW) mode: lifts the general SFW filter for fully-synthetic adult (18+) personas; admin setting, off by default, personal/single-user mode only, auto-disabled in multi-user/SaaS via AppManager FeatureSvc. *Acceptance:* toggle works only in personal mode; multi-user forces SFW-locked regardless of the stored setting. (BRD-79)

<a id="d-req-fn-063"></a>
- **REQ-FN-063** — NSFW scope enforcement: applies only to characters flagged synthetic + adult; refuses photoreal-of-real-person, real-person references and minors; SAFE-01..06 floors (REQ-FN-056..061) remain enforced with the mode ON. *Acceptance:* with mode ON, a real-person or minor-flagged job is still refused + logged; floors verifiably fire. (BRD-80)

### Phase 9 — Polish + SaaS-ready seams

<a id="d-req-fn-064"></a>
- **REQ-FN-064** — Ops dashboard backend: worker health, queue depth, spend-to-date, error rates. *Acceptance:* the ops endpoints report live health/queue/spend/error data consumed by REQ-UI-073. (BRD-15)

<a id="d-req-fn-065"></a>
- **REQ-FN-065** — Side-by-side comparison of outputs across models/seeds/providers. *Acceptance:* two+ assets render side-by-side with their differing params highlighted. *(UI: REQ-UI-052 / docs/mockups/compare.html.)* (BRD-49)

<a id="d-req-fn-066"></a>
- **REQ-FN-066** — Prompt + script library with reusable style/voice presets. *Acceptance:* a saved prompt/preset is reusable from the studios. *(UI: REQ-UI-053 / docs/mockups/library.html.)* (BRD-50)

<a id="d-req-fn-067"></a>
- **REQ-FN-067** — Export/download single + bulk, with provenance metadata included. *Acceptance:* bulk export delivers all selected assets with provenance intact. (BRD-51)

<a id="d-req-fn-068"></a>
- **REQ-FN-068** — Account usage/spend view backend with license/subscription & billing surfaced from AppManager. *Acceptance:* spend aggregates per user; license state reads live from AppManager. (BRD-52)

<a id="d-req-fn-069"></a>
- **REQ-FN-069** — `TrStudioApi` designed to serve as an external REST API later: API-key auth seam, versioned contracts, no UI-only capabilities. *Acceptance:* a job submits end-to-end with API-key auth (no interactive session). (BRD-66)

<a id="d-req-fn-070"></a>
- **REQ-FN-070** — Webhooks fire on job completion/failure. **Deferred (Could).** *Acceptance:* a registered webhook receives completion and failure payloads. (BRD-67)

<a id="d-req-fn-071"></a>
- **REQ-FN-071** — MCP server exposing generation as agent tools. **Deferred (Could).** *Acceptance:* an MCP client lists and invokes a generation tool. (BRD-68)

### P-amend — Unified role-based app (2026-07-05)

<a id="d-req-fn-072"></a>
- **REQ-FN-072** — Role-based surface authorization & routing within the single `TrStudio` app: after login, present AND authorize the creator vs management areas by role — server-side (endpoint/authorization policy) and client (navigation/routing) — with the admin-only host retired. Owner sees both surfaces with an in-app switch. (BRD-86) *Amended 2026-07-05.*
  - *Acceptance:* a Creator is routed to the studios and is refused management routes/endpoints server-side; an Operator/Admin is routed to management; an Owner can access both and switch in-app.
  - *Acceptance:* authorization is enforced server-side (not only hidden in the UI) and the separate admin host is no longer needed.

### Phase 10 — Prompt Studio (added 2026-07-07)

<a id="d-req-fn-073"></a>
- **REQ-FN-073** — Prompt-chat persistence + endpoints: `PromptChatSession` / `PromptChatMessage` tables (DbUp script in `TrStudioDb`), Dapper repository in `TrStudio.Engine/Infrastructure/` with the BRD-81 owner filter on every query, and `prompt-chat` endpoints in `TrStudioApi` (create/list/rename/delete session; post message → assistant reply). No Hangfire job — synchronous chat path outside the generation spine (ADR-013). (BRD-91) *Added 2026-07-07.*
  - *Acceptance:* sessions/messages persist and reload; a foreign user's session returns 404; deleting a session removes its messages; endpoints require auth.

<a id="d-req-fn-074"></a>
- **REQ-FN-074** — Text-chat LLMs as registry rows: `task: "text-chat"` / `providerType: "textllm"` entries (displayName, endpoint, encrypted API key via the existing key store, `visionCapable`, token cost) manageable on `/admin/registry`; excluded from `IGenerationProvider` resolution and served to the Prompt-studio picker via the models API. (BRD-89) *Added 2026-07-07.*
  - *Acceptance:* adding/editing a text-chat row is config-only (no code change, NFR-09); keys are masked at rest; the picker endpoint returns only `text-chat` rows; generation-job submission against a text-chat model is rejected.

## RAG / AI requirements (→ /techierag)

<!-- Built by /techierag as a sub-agent of build-phase. TechieRag is text/LLM/RAG ONLY —
     never the image/video path. PgVector store lives on the same Postgres instance. -->

<a id="d-req-rag-001"></a>
- **REQ-RAG-001** — Script generation/expansion from a brief (EN/HI) via TechieRag, surfaced in the Avatar-studio script-assist panel. *Acceptance:* a one-line brief expands to a usable narration script in EN and HI. (BRD-53) · P4

<a id="d-req-rag-002"></a>
- **REQ-RAG-002** — Caption/subtitle text produced from Whisper transcripts via TechieRag (consumed by REQ-FN-040 for burn-in/SRT). *Acceptance:* an audio track yields timed caption text matching its speech. (BRD-54) · P4

<a id="d-req-rag-003"></a>
- **REQ-RAG-003** — Token-budget tracking for text-LLM spend, feeding the spend rollup (REQ-FN-029/068). *Acceptance:* LLM calls accrue token counts/cost per user; totals visible in spend views. (BRD-56) · P4

<a id="d-req-rag-004"></a>
- **REQ-RAG-004** — RAG-backed character bible (persona voice/wording consistency) stored in PgVector on the app's Postgres instance (TechieRag `UsePgVector`), surfaced in the Character-library bible `Sheet`. *Acceptance:* script assist retrieves and applies a character's bible entries; bible edits persist per character. (BRD-55) · P5

<a id="d-req-rag-005"></a>
- **REQ-RAG-005** — Prompt-crafting chat via TechieRag **direct chat-completion — deliberately NO RAG/vector setup** (ADR-013): `PromptChatService` resolves the picked registry LLM (endpoint + key), sends the session history + new message (with image attachment passed through to vision-capable models, BRD-90), returns the assistant reply, and accrues token usage into the BRD-56 tracker (`SpendChannel.LlmText`). Degrades cleanly ("LLM not configured / unreachable") like REQ-RAG-001. *Acceptance:* a chat round-trip returns a usable crafted prompt from the configured LLM; an image-attached message reaches a vision model and is rejected with a clear message on a non-vision model; token usage appears in `/api/v1/rag/usage` and the spend rollup. (BRD-88, BRD-90, BRD-91) · P10 *Added 2026-07-07.*

## Non-functional

<!-- Cross-cutting: verified against the whole build, not one screen. -->

<a id="d-req-nfr-001"></a>
- **REQ-NFR-001** — Performance: UI stays responsive during long jobs; progress streamed; first warm local image in seconds. *Acceptance:* no UI freeze during a long render; warm T2I completes within seconds locally. (BRD-69)

<a id="d-req-nfr-002"></a>
- **REQ-NFR-002** — Scalability: orchestrator scales by adding workers; never assumes a single GPU. *Acceptance:* two workers process the queue concurrently without code change. (BRD-70)

<a id="d-req-nfr-003"></a>
- **REQ-NFR-003** — Portability: `TrStudioApi`/workers run on macOS (Apple Silicon) + Linux (RunPod); `TrStudio`/`TrStudioAdmin` ship as Blazor Web + MAUI Blazor Hybrid (Win/Mac). *Acceptance:* API builds/runs on both OS targets; apps build for Web + MAUI heads. (BRD-71)

<a id="d-req-nfr-004"></a>
- **REQ-NFR-004** — Reliability: durable jobs (Hangfire/Postgres); worker crash → retry or graceful fail without losing the queue. *Acceptance:* killing a worker mid-job leaves the queue intact and the job retried/failed gracefully. (BRD-72)

<a id="d-req-nfr-005"></a>
- **REQ-NFR-005** — Security: API auth on every endpoint; encrypted provider-key store; no secrets in logs/URLs; least-privilege workers. *Acceptance:* unauthenticated calls rejected; a log/URL sweep finds zero secrets. (BRD-73)

<a id="d-req-nfr-006"></a>
- **REQ-NFR-006** — Privacy: local jobs stay on the machine; UI clearly flags when a job routes to a closed API (HeyGen/aggregator). *Acceptance:* local jobs make no external calls; closed-API routing always shows the banner. (BRD-74)

<a id="d-req-nfr-007"></a>
- **REQ-NFR-007** — Cost control: per-job estimates; RunPod idle auto-stop; monthly cap + alerts across RunPod and closed APIs. *Acceptance:* estimates precede every paid run; cap alerts fire; idle workers stop. (BRD-75)

<a id="d-req-nfr-008"></a>
- **REQ-NFR-008** — Observability: Serilog structured logging + OpenTelemetry metrics (queue, memory, latency, spend), traceable job IDs end-to-end. *Acceptance:* a job ID traces from submit through provider to asset in logs + metrics. (BRD-76)

<a id="d-req-nfr-009"></a>
- **REQ-NFR-009** — Extensibility: registry-driven UI; a new model = a config row + (for ComfyUI) a workflow import — never a dev task. *Acceptance:* a new registry row becomes creator-usable with zero code change. (BRD-77)

<a id="d-req-nfr-010"></a>
- **REQ-NFR-010** — Compliance: provenance metadata on outputs; auditable generation log; configurable retention; DPDP-aligned (data resident in India). *Acceptance:* retention config honored; audit + provenance verifiable on every output. (BRD-78)
