# TrStudio — Checklist

| | |
|---|---|
| App | TrStudio |
| Size | Large |
| Phase | 1 of 2 |

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
| REQ-UI-001 | Login screen (`/login`) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-UI-001 REQ-UI-002 REQ-UI-003 REQ-UI-004 every screen wit`; Login renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-ui-001) |
| REQ-UI-002 | Register screen (`/register`) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-UI-001 REQ-UI-002 REQ-UI-003 REQ-UI-004 every screen wit`; Register renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-ui-002) |
| REQ-UI-003 | Forgot-password screen (`/forgot-password`) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-UI-001 REQ-UI-002 REQ-UI-003 REQ-UI-004 every screen wit`; Forgot password renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-ui-003) |
| REQ-UI-004 | Setup-required screen (`/setup-required`) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-UI-001 REQ-UI-002 REQ-UI-003 REQ-UI-004 every screen wit`; Setup required (first-run gate) renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-ui-004) |
| REQ-UI-005 | App shell & brand theme (TrStudioUI RCL) | Verified | 100% | 2026-11-19 verify: PASS — test `UI sweep REQ-UI-005 REQ-UI-010 REQ-UI-011 REQ-UI-020 REQ-UI-`; Avatar studio renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 1 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-ui-005) |
| REQ-UI-010 | Avatar studio (`/avatar`) | Verified | 100% | 2026-11-19 verify: PASS — test `UI sweep REQ-UI-005 REQ-UI-010 REQ-UI-011 REQ-UI-020 REQ-UI-`; Avatar studio renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 1 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-ui-010) |
| REQ-UI-011 | Character library (`/avatar/characters`) | Verified | 100% | 2026-11-19 verify: PASS — test `UI sweep REQ-UI-005 REQ-UI-010 REQ-UI-011 REQ-UI-020 REQ-UI-`; Character library renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-ui-011) |
| REQ-UI-020 | Image studio (`/image`) | Verified | 100% | 2026-11-19 verify: PASS — test `UI sweep REQ-UI-005 REQ-UI-010 REQ-UI-011 REQ-UI-020 REQ-UI-`; Image studio renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-ui-020) |
| REQ-UI-030 | Voice studio (`/voice`) | Verified | 100% | 2026-11-19 verify: PASS — test `UI sweep REQ-UI-005 REQ-UI-010 REQ-UI-011 REQ-UI-020 REQ-UI-`; Voice studio renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-ui-030) |
| REQ-UI-040 | Video studio (`/video`) | Needs re-verify | 75% | 2026-11-20 verify: ⚠ mockup-parity — missing on card-controls > div[0] > div[1] @1280: the mockup draws a badge/pill here (""); it could not be located in the app — no… ⚠ UAT bug 2026-11-18: Model box cut a long seeded model name at 1280 px (owner screen check 17 Nov); fixed with a wrapping label, check video-model-fit.spec.ts (evidence: tests/.artifacts/verify/video-model-box-1280.png; kind:… | [view](#d-req-ui-040) |
| REQ-UI-050 | Gallery (`/gallery`) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-UI-050 AssetListReadsZeroAndFractionalRunCosts`; Gallery renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 4 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-ui-050) |
| REQ-UI-051 | Projects (`/projects`) | Verified | 100% | 2026-11-19 verify: PASS — test `UI sweep REQ-UI-005 REQ-UI-010 REQ-UI-011 REQ-UI-020 REQ-UI-`; Projects renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-ui-051) |
| REQ-UI-052 | Comparison view (`/compare`) | Verified | 100% | 2026-11-20 verify: PASS — test `UI sweep REQ-UI-005 REQ-UI-010 REQ-UI-011 REQ-UI-020 REQ-UI-`; Comparison renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 9 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-ui-052) |
| REQ-UI-053 | Prompt/script library (`/library`) | Verified | 100% | 2026-11-19 verify: PASS — test `UI sweep REQ-UI-005 REQ-UI-010 REQ-UI-011 REQ-UI-020 REQ-UI-`; Library renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-ui-053) |
| REQ-UI-060 | Account & spend (`/account`) | Verified | 100% | 2026-11-19 verify: PASS — test `UI sweep REQ-UI-005 REQ-UI-010 REQ-UI-011 REQ-UI-020 REQ-UI-`; Account & spend renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 6 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-ui-060) |
| REQ-UI-070 | Admin — Hardware & guided setup (`/admin/setup`) | Verified | 100% | 2026-11-19 verify: PASS — test `UI sweep REQ-UI-070 REQ-UI-071 REQ-UI-072 REQ-UI-073 REQ-UI-`; Admin — Hardware & setup renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-ui-070) |
| REQ-UI-071 | Admin — Registry & providers (`/admin/registry`) | Verified | 100% | 2026-11-19 verify: PASS — test `UI sweep REQ-UI-070 REQ-UI-071 REQ-UI-072 REQ-UI-073 REQ-UI-`; Admin — Model & Provider Registry renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-ui-071) |
| REQ-UI-072 | Admin — ComfyUI import & debug (`/admin/comfy/import`) | Verified | 100% | 2026-11-19 verify: PASS — test `UI sweep REQ-UI-070 REQ-UI-071 REQ-UI-072 REQ-UI-073 REQ-UI-`; Admin — ComfyUI import & debug renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-ui-072) |
| REQ-UI-073 | Admin — RunPod & ops (`/admin/ops`) | Verified | 100% | 2026-11-19 verify: PASS — test `UI sweep REQ-UI-070 REQ-UI-071 REQ-UI-072 REQ-UI-073 REQ-UI-`; Admin — RunPod & ops renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 5 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-ui-073) |
| REQ-UI-074 | Admin — Model sources (`/admin/sources`) | Verified | 100% | 2026-11-19 verify: PASS — test `UI sweep REQ-UI-070 REQ-UI-071 REQ-UI-072 REQ-UI-073 REQ-UI-`; Admin — Model Sources renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-ui-074) |
| REQ-UI-075 | Admin — Content policy / NSFW (`/admin/policy`) | Verified | 100% | 2026-11-19 verify: PASS — test `UI sweep REQ-UI-070 REQ-UI-071 REQ-UI-072 REQ-UI-073 REQ-UI-`; Admin — Content policy (NSFW) renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-ui-075) |
| REQ-UI-076 | Admin — Model library (`/admin/models`) | Verified | 100% | 2026-11-19 verify: PASS — test `UI sweep REQ-UI-070 REQ-UI-071 REQ-UI-072 REQ-UI-073 REQ-UI-`; Admin — Model Library renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 5 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-ui-076) |
| REQ-UI-080 | Unified role-based app shell (merge admin surface into TrStudio) | Verified | 100% | 2026-11-19 verify: PASS — test `UI sweep REQ-FN-072 REQ-UI-080 Creator is refused the admin `; Admin — RunPod & ops renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 5 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-ui-080) |
| REQ-UI-081 | Prompt studio — chat screen (`/prompt-studio`) | Verified | 100% | 2026-11-19 verify: PASS — test `Prompt Studio verify REQ-UI-081 REQ-UI-082 prompt studio ren`; Prompt studio renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 1 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-ui-081) |
| REQ-UI-082 | Prompt studio — sessions & prompt-use actions | Verified | 100% | 2026-11-19 verify: PASS — test `Prompt Studio verify REQ-UI-081 REQ-UI-082 prompt studio ren`; Prompt studio renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 1 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-ui-082) |
| REQ-FN-001 | `IGenerationProvider` abstraction | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-001 StubJobRunsEndToEndAndStoresAsset` | [view](#d-req-fn-001) |
| REQ-FN-002 | Async job model (submit→poll/stream→fetch) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-002 SameStatusSequenceForTwoProviders` | [view](#d-req-fn-002) |
| REQ-FN-003 | Hangfire/Postgres durable job queue | N/A | 0% | removed 2026-11-14: replaced by the in-app job runner, phase 3 REQ-FN-091 to REQ-FN-095; Hangfire dashboard set aside (no web server). | [view](#d-req-fn-003) |
| REQ-FN-004 | Unified `TrStudioApi` surface (OpenAPI, auth, versioned) | N/A | 0% | removed 2026-11-14: replaced by the in-process routes, phase 3 REQ-NFR-016 and REQ-NFR-017; OpenAPI page and API host set aside (no web server). | [view](#d-req-fn-004) |
| REQ-FN-005 | SignalR progress + actionable failures | N/A | 0% | removed 2026-11-14: replaced by in-app job status the screens read, phase 3 REQ-FN-091; SignalR hub set aside (no web server). | [view](#d-req-fn-005) |
| REQ-FN-006 | Model & provider registry + declarative params schema | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-006 OperatorRegistryRowIsSelectableInStudio` | [view](#d-req-fn-006) |
| REQ-FN-007 | Closed-provider config + encrypted key store | N/A | 0% | removed 2026-11-14: replaced by keys protected by the operating system, phase 3 REQ-FN-096 to REQ-FN-098. | [view](#d-req-fn-007) |
| REQ-FN-008 | Host capability detection | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-008 OperatorSeesHostReadinessReport` | [view](#d-req-fn-008) |
| REQ-FN-009 | Guided setup (ONNX + connect to a running ComfyUI on Mac) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-009 ConnectKeepsAddressOfARunningComfyUi` | [view](#d-req-fn-009) |
| REQ-FN-010 | Managed isolated Python/ComfyUI env (Windows); none on Mac | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-010 PythonProvisionTouchesOnlyManagedPath` | [view](#d-req-fn-010) |
| REQ-FN-011 | Model install service (resumable, size/VRAM/licence) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-011 InterruptedDownloadResumesWhereItStopped` | [view](#d-req-fn-011) |
| REQ-FN-012 | AppManager login (JWT + refresh, RSA passwords) | N/A | 0% | removed 2026-11-14: replaced by sign-in with the built-in application id and API key, phase 3 REQ-FN-086. | [view](#d-req-fn-012) |
| REQ-FN-013 | License/subscription validation (LicenseSvc) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-013 AccountShowsLicenceFromAppManager` | [view](#d-req-fn-013) |
| REQ-FN-014 | Feature/tier gating (FeatureSvc) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-014 PremiumWithoutLicenceRefusedWithUpgradePrompt`; Avatar studio renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 1 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-fn-014) |
| REQ-FN-015 | Registration via AppManager + superadmin bootstrap | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-015 RegisteredVisitorCanSignIn`; Register renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-fn-015) |
| REQ-FN-016 | Role-based default landing after login | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-016 OperatorLandsOnOps`; Login renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-fn-016) |
| REQ-FN-017 | First-run setup gate (sources + ready model check) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-017 CreatorBeforeSetupGoesToSetupRequired` | [view](#d-req-fn-017) |
| REQ-FN-018 | Generation log recording | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-018 FinishedJobsWriteCompleteLogRows` | [view](#d-req-fn-018) |
| REQ-FN-019 | Text-to-image generation (OnnxLocalProvider) | N/A | 0% | deferred 2026-11-19 (later version, owner decision 18C): joins BRD-159 / REQ-FN-103, local pictures on Mac; Mac pictures come from ComfyUI, RunPod or Venice. Its local-model test waits for that version. | [view](#d-req-fn-019) |
| REQ-FN-020 | Image-to-image / inpaint / outpaint / mask | N/A | 0% | deferred 2026-11-19 (later version, owner decision 18C): joins BRD-159 / REQ-FN-103, local pictures on Mac. Its local-model test waits for that version. | [view](#d-req-fn-020) |
| REQ-FN-021 | ControlNet / reference conditioning + identity lock | N/A | 0% | deferred 2026-11-19 (later version, owner decision 18C): joins BRD-159 / REQ-FN-103, local pictures on Mac. Its local-model test waits for that version. | [view](#d-req-fn-021) |
| REQ-FN-022 | Upscale + face restoration (2K/4K) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-022 UpscaleTo4KHasRequestedResolution`; Image studio renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-fn-022) |
| REQ-FN-023 | Batch/grid variations + seed reproducibility | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-023 SameSeedAndParamsGiveIdenticalImage`; Image studio renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-fn-023) |
| REQ-FN-024 | Social-format export + background removal | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-024 Export916PresetHas916Ratio`; Image studio renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-fn-024) |
| REQ-FN-025 | Asset gallery backend (browse/filter/reuse) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-025 GalleryAssetSeedsNewJob`; Gallery renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 4 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-fn-025) |
| REQ-FN-026 | Projects + parameter history + reproduce | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-026 ReproduceByHistoryRowJobIdIsAccepted` | [view](#d-req-fn-026) |
| REQ-FN-027 | Per-user isolation (server-side owner filter) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-027 OtherUsersAssetByIdIsNotFound` | [view](#d-req-fn-027) |
| REQ-FN-028 | RunPod serverless integration (scale-to-zero) | Needs re-verify | 75% | 2026-11-19 verify: not measured — 1 test(s) named REQ-FN-028 passed and 1 skipped (`REQ-FN-028 HighTierJobStartsAndStopsLiveRunPodWork`: unit test skipped: REQ-FN-028 HighTierJobStartsAndStopsLiveR); a skipped test is part of the row's acceptance, so it is not measured until that test runs; screen Avatar studio renders and looks right; mockup-parity: 1 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script… | [view](#d-req-fn-028) |
| REQ-FN-029 | Cost guardrails (estimate, idle-stop, monthly cap) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-029 OverCapJobIsBlockedWithOverCapMessage`; Avatar studio renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 1 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-fn-029) |
| REQ-FN-030 | Model-repository sources (HF/Civitai/custom) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-030 EnabledSourceIsSavedTestedAndInstallable` | [view](#d-req-fn-030) |
| REQ-FN-031 | TTS EN/HI/Hinglish with preset voices | N/A | 0% | deferred 2026-11-20 (later version, owner decision 18C): joins BRD-180 / REQ-FN-117, local voice, lip-sync and caption models on Mac. Its local-voice test waits for that version. | [view](#d-req-fn-031) |
| REQ-FN-032 | Voice cloning (consent-gated for real people) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-032 RealPersonCloneWithoutConsentIsRefused` | [view](#d-req-fn-032) |
| REQ-FN-033 | Per-language voice model routing | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-033 HindiPicksTheHindiVoiceModel`; Voice studio renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-fn-033) |
| REQ-FN-034 | Voice emotion/style/speed controls | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-034 VoiceWithoutEmotionHasEmotionDisabled`; Voice studio renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-fn-034) |
| REQ-FN-035 | Audio→avatar alignment handoff | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-035 ClipSentToAvatarCarriesItsTiming` | [view](#d-req-fn-035) |
| REQ-FN-036 | Character entity + identity lock | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-036 SavedCharacterReusesLockedIdentityPerUser`; Avatar studio renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 1 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-fn-036) |
| REQ-FN-037 | Talking-avatar generation (lip-sync routing) | N/A | 0% | deferred 2026-11-20 (later version, owner decision 18C): joins BRD-180 / REQ-FN-117, local voice, lip-sync and caption models on Mac. Its local lip-sync test waits for that version. | [view](#d-req-fn-037) |
| REQ-FN-038 | Script→video end-to-end pipeline | N/A | 0% | deferred 2026-11-20 (later version, owner decision 18C): joins BRD-180 / REQ-FN-117, local voice, lip-sync and caption models on Mac. Its script-to-narrated-clip test waits for that version. | [view](#d-req-fn-038) |
| REQ-FN-039 | Fast/High/Premium tier routing | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-039 ChangingOnlyTierRoutesToDifferentProvider`; Avatar studio renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 1 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-fn-039) |
| REQ-FN-040 | Captions/subtitles EN/HI (burn-in or SRT) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-040 CaptionsExportAsSrtMatchingTheScript(aLanguage: "`; Avatar studio renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 1 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-fn-040) |
| REQ-FN-043 | ComfyUI workflow import + parse | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-043 ImportListsNodesAndInputs` | [view](#d-req-fn-043) |
| REQ-FN-044 | Workflow input → registry-param mapping | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-044 MappedInputsBecomeExactlyTheParams` | [view](#d-req-fn-044) |
| REQ-FN-045 | Run workflow on chosen worker + WS previews | Implemented | 75% | 2026-11-19 verify: not measured — every test named REQ-FN-045 was skipped; acceptance not measured (1 test(s) skipped, none ran) | [view](#d-req-fn-045) |
| REQ-FN-046 | ComfyUI debug view (per-node status, exact JSON) | Implemented | 75% | 2026-11-19 verify: not measured — 2 test(s) named REQ-FN-046 passed and 1 skipped (`REQ-FN-046 FailingNodeShowsItsError`: unit test skipped: REQ-FN-046 FailingNodeShowsItsError); a skipped test is part of the row's acceptance, so it is not measured until that test runs | [view](#d-req-fn-046) |
| REQ-FN-047 | Auto-install custom nodes + model files | Needs re-verify | 75% | 2026-11-19 verify: not measured — 3 test(s) named REQ-FN-047 passed and 1 skipped (`REQ-FN-047 MissingCustomNodeIsInstalled`: unit test skipped: REQ-FN-047 MissingCustomNodeIsInstalled); a skipped test is part of the row's acceptance, so… ⚠ UAT bug 2026-11-18: UAT bug 16 Nov fixed: the dependency scan never asked ComfyUI what it holds, so every named model file and custom node was reported missing; the… | [view](#d-req-fn-047) |
| REQ-FN-048 | Save workflow as registered model | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-048 SavedWorkflowUsableWithoutGraph` | [view](#d-req-fn-048) |
| REQ-FN-049 | Workflow versioning + known-good pin | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-049 NewVersionKeepsPinnedServed` | [view](#d-req-fn-049) |
| REQ-FN-050 | Sandbox smoke test before live | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-050 PassingSmokeAllowsGoLive` | [view](#d-req-fn-050) |
| REQ-FN-051 | I2V/T2V generation (open + closed) | Needs re-verify | 75% | 2026-11-20 verify: ⚠ mockup-parity — missing on card-controls > div[0] > div[1] @1280: the mockup draws a badge/pill here (""); it could not be located in the app — no… ⚠ UAT bug 2026-11-18: Video studio's image-to-video sent only the start frame's file name, never the picture (same cause as REQ-FN-052); fixed. Live Venice clip 18 Nov: asked 2 s… | [view](#d-req-fn-051) |
| REQ-FN-052 | First/last-frame + reference conditioning | Needs re-verify | 75% | 2026-11-20 verify: ⚠ mockup-parity — missing on card-controls > div[0] > div[1] @1280: the mockup draws a badge/pill here (""); it could not be located in the app — no (tests/.artifacts/verify/parity.json) | [view](#d-req-fn-052) |
| REQ-FN-053 | Frame interpolation + video upscale | N/A | 0% | deferred 2026-11-19 (later version, owner decision 18C): waits for a frame-interpolation model; today the step only answers "queued". | [view](#d-req-fn-053) |
| REQ-FN-054 | Video edit (trim/join/reframe/audio/export) | Needs re-verify | 75% | 2026-11-20 verify: ⚠ mockup-parity — missing on card-controls > div[0] > div[1] @1280: the mockup draws a badge/pill here (""); it could not be located in the app — no (tests/.artifacts/verify/parity.json) | [view](#d-req-fn-054) |
| REQ-FN-056 | CSAM prevention (hash + classifier, hard block) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-056 FlaggedInputIsBlockedAndAuditedOnEveryPath` | [view](#d-req-fn-056) |
| REQ-FN-057 | NCII / real-person sexualisation block | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-057 RealPersonSexualJobIsRefusedAndLogged` | [view](#d-req-fn-057) |
| REQ-FN-058 | Recorded consent gate (real face/voice) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-058 RealPersonFaceCloneBlockedUntilConsentRecorded` | [view](#d-req-fn-058) |
| REQ-FN-059 | Provenance / AI-disclosure metadata (C2PA) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-059 ExportedAssetCarriesProvenanceManifest` | [view](#d-req-fn-059) |
| REQ-FN-060 | Public-figure impersonation block | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-060 PublicFigureJobWithoutRightsIsRefused` | [view](#d-req-fn-060) |
| REQ-FN-061 | Policy config + abuse reporting + audit trail | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-061 AbuseReportPersistsAndGenerationsAreTraceable` | [view](#d-req-fn-061) |
| REQ-FN-062 | Owner-only Unrestricted (NSFW) mode toggle | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-062 MultiUserModeForcesUnrestrictedToggleOff` | [view](#d-req-fn-062) |
| REQ-FN-063 | NSFW scope enforcement (synthetic+adult only) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-063 MinorFlaggedAvatarJobRefusedWithUnrestrictedOn` | [view](#d-req-fn-063) |
| REQ-FN-064 | Ops dashboard backend (health/queue/spend/errors) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-064 OpsDashboardShowsLiveHealthQueueAndSpend` | [view](#d-req-fn-064) |
| REQ-FN-065 | Side-by-side comparison | Verified | 100% | 2026-11-20 verify: PASS — test `REQ-FN-065 ComparisonReturnsTwoAssetsWithDifferingParams`; Comparison renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 9 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-fn-065) |
| REQ-FN-066 | Prompt/script library + presets | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-066 SavedPromptPresetIsReusableFromLibrary`; Library renders and looks right @1280/390 in light and dark mode; matches its mockup | [view](#d-req-fn-066) |
| REQ-FN-067 | Export/download (single + bulk) with provenance | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-067 BulkExportDeliversEveryAssetWithProvenance`; Gallery renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 4 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-fn-067) |
| REQ-FN-068 | Account usage/spend + AppManager license view | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-068 AccountTotalsSpendPerUserAndReadsLiveLicence` | [view](#d-req-fn-068) |
| REQ-FN-069 | External REST API seam (API-key auth) | N/A | 0% | set aside 2026-11-14 (deferred): outside callers need a web server, and the desktop app has none; kept in the API project, unused. | [view](#d-req-fn-069) |
| REQ-FN-070 | Webhooks on completion/failure | N/A | 0% | deferred 2026-11-17 (Could, owner decision 14A confirmed): its test waits for the feature and is skipped, not failed. | [view](#d-req-fn-070) |
| REQ-FN-071 | MCP server (generation as agent tools) | N/A | 0% | deferred 2026-11-17 (Could, owner decision 14A confirmed): its test waits for the feature and is skipped, not failed. | [view](#d-req-fn-071) |
| REQ-FN-072 | Role-based surface authorization & routing (single app) | Verified | 100% | 2026-11-19 verify: PASS — test `UI sweep REQ-FN-072 REQ-UI-080 Creator is refused the admin ` | [view](#d-req-fn-072) |
| REQ-FN-073 | Prompt-chat sessions/messages persistence (per-user) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-073 PromptChatSessionIsHiddenFromAnotherUser` | [view](#d-req-fn-073) |
| REQ-FN-074 | Registry `text-chat` modality + admin LLM config | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-FN-074 OperatorTextChatRowAppearsInPromptPicker` | [view](#d-req-fn-074) |
| REQ-RAG-001 | Script generation/expansion from brief (EN/HI) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-RAG-001 OneLineBriefExpandsToEnglishOrHindiScript` | [view](#d-req-rag-001) |
| REQ-RAG-002 | Caption/subtitle text from Whisper transcripts | N/A | 0% | deferred 2026-11-20 (later version, owner decision 18C): joins BRD-180 / REQ-FN-117, local voice, lip-sync and caption models on Mac. Its Whisper test waits for that version. | [view](#d-req-rag-002) |
| REQ-RAG-003 | Token-budget tracking for text-LLM spend | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-RAG-003 LlmTokensAndCostAddToCreatorSpend` | [view](#d-req-rag-003) |
| REQ-RAG-004 | RAG-backed character bible (PgVector) | N/A | 0% | removed 2026-11-14: replaced by bible search on the local SQLite store through TechieRag, phase 3 REQ-RAG-007. | [view](#d-req-rag-004) |
| REQ-RAG-005 | Prompt-crafting chat via TechieRag chat-completion (no RAG) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-RAG-005 UnconfiguredModelDegradesWithUsableReply`; Prompt studio renders and looks right @1280/390 in light and dark mode; matches its mockup; mockup-parity: 1 sample box(es) not measured, the app drew no data there (a tests/verify/seed/ script puts it in the drawn state) | [view](#d-req-rag-005) |
| REQ-NFR-001 | Performance (responsive UI, streamed progress) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-NFR-001 LongRenderKeepsScreensAnsweringAndProgressStream` | [view](#d-req-nfr-001) |
| REQ-NFR-002 | Scalability (add workers, no single-GPU assumption) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-NFR-002 TwoWorkersProcessJobsConcurrently` | [view](#d-req-nfr-002) |
| REQ-NFR-003 | Portability (macOS+Linux API; Web+MAUI apps) | N/A | 0% | removed 2026-11-19 (owner decision 19A): the API on macOS and Linux and the web head were set aside (decision 8); replaced by phase 3's REQ-NFR-022 (the Mac build), Verified. | [view](#d-req-nfr-003) |
| REQ-NFR-004 | Reliability (durable jobs, crash retry) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-NFR-004 WorkerKilledMidJobLeavesQueueIntactAndJobFailsCl` | [view](#d-req-nfr-004) |
| REQ-NFR-005 | Security (auth, encrypted keys, no secret leaks) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-NFR-005 LogsAndUrlsHoldNoSecretAfterARun` | [view](#d-req-nfr-005) |
| REQ-NFR-006 | Privacy (local stays local; closed-API routing flagged) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-NFR-006 LocalJobMakesNoExternalCall` | [view](#d-req-nfr-006) |
| REQ-NFR-007 | Cost control (estimates, idle-stop, cap + alerts) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-NFR-007 PaidJobShowsEstimateBeforeRun` | [view](#d-req-nfr-007) |
| REQ-NFR-008 | Observability (Serilog + OpenTelemetry, job IDs) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-NFR-008 JobIdLinksSubmitProviderAndAssetInLogsAndMetrics` | [view](#d-req-nfr-008) |
| REQ-NFR-009 | Extensibility (registry-driven UI; model = config) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-NFR-009 NewRegistryRowIsCreatorUsableWithZeroCodeChange` | [view](#d-req-nfr-009) |
| REQ-NFR-010 | Compliance (provenance, audit, retention of log rows, DPDP) | Verified | 100% | 2026-11-19 verify: PASS — test `REQ-NFR-010 RetentionRemovesOldLogRowsAndKeepsEveryAsset` | [view](#d-req-nfr-010) |

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
  - *Acceptance:* When a user signs in with valid AppManager credentials on Login, then they land on their role's default page.
  - *Also checked:* invalid fields show `FieldError`; submit disabled until valid; auth failure shows `Alert` (Danger); successful login redirects per role (Owner/Creator → `/avatar`, Operator → `/admin/ops`) or into the first-run gate; controls do not overlap at desktop + mobile widths (visual gate).

### Page: Register (`/register`)

<a id="d-req-ui-002"></a>
- **REQ-UI-002** — Account creation via the AppManager child-app API: name/email/password/confirm, account-type selector (Creator / Admin-Operator) that decides the provisioned role and the post-signup landing, duplicate-email `Alert`. (BRD-83, BRD-84) *Mockup:* docs/mockups/register.html.
  - *Acceptance:* When a visitor submits matching passwords on Register, then the account is created and they reach their landing page.
  - *Also checked:* password mismatch → `FieldError`; submit disabled until valid; account type drives the landing (Creator → `/avatar`, Admin → setup landing); AppManager error surfaces as `Alert`; no local password store; visual gate at both widths.

### Page: Forgot password (`/forgot-password`)

<a id="d-req-ui-003"></a>
- **REQ-UI-003** — Password-reset request screen: email field + "Send reset link" (AppManager API), confirmation `Alert` (Success), back-to-login link; reset itself completes through AppManager. (BRD-57) *Mockup:* docs/mockups/forgot-password.html.
  - *Acceptance:* When a user submits a valid email on Forgot password, then a success alert says a reset link was sent.
  - *Also checked:* submit disabled until valid email; sent state shows success `Alert`; no local reset flow; visual gate at both widths.

### Page: Setup required (`/setup-required`)

<a id="d-req-ui-004"></a>
- **REQ-UI-004** — Creator-facing first-run gate screen: "TrStudio isn't set up yet" card (no sidebar), ask-your-admin copy, "Sign in as admin" CTA + "Check again" re-check, 3-step admin hint card. Shown only while the BRD-85 gate fails; admins are never sent here. (BRD-85) *Mockup:* docs/mockups/setup-required.html.
  - *Acceptance:* When a Creator signs in before setup completes, then Setup required shows with a Check again button.
  - *Also checked:* renders for a Creator when no enabled source or no ready model exists; "Check again" re-evaluates and forwards to `/avatar` once setup completes; visual gate at both widths.

### Page: App shell & brand theme (TrStudioUI RCL)

<a id="d-req-ui-005"></a>
- **REQ-UI-005** — The shared shell + theme both apps consume from `TrStudioUI`: collapsible `Sidebar` shell (`SidebarProvider`/`SidebarInset`) with topbar (`SidebarTrigger`, breadcrumb, theme toggle, user chip), dark-by-default OKLCH theme with persisted light/dark toggle, the violet→fuchsia brand-gradient solid `Button` default (no monochrome solids), `.brand-gradient` utility, control-state token mapping and LucideIcon map per the UIDesign Branding guide; creator + admin nav sets wired incl. footer app cross-link. (BRD-71; UIDesign §Design system + §Branding guide) *Mockup:* docs/mockups/avatar-studio.html (shell shared by every screen).
  - *Acceptance:* When any signed-in user opens a page in the app, then the shared sidebar, topbar and dark brand theme render.
  - *Also checked:* both apps render the same shell from the RCL; dark is default and the toggle persists (localStorage) across pages; base solid `Button` renders the brand gradient in light + dark; sidebar nav items route correctly in both apps; visual gate at both widths.

### Page: Avatar studio (`/avatar`)

<a id="d-req-ui-010"></a>
- **REQ-UI-010** — Three-column working surface: character picker (empty-state CTA), voice picker + generate, script editor with TechieRag assist, preview player with render `Progress`, Fast/High/Premium `RadioGroup` tier selector with per-tier cost/eta + HeyGen `Badge`, closed-API routing `Alert`, job/cost panel with pre-run estimate + over-cap warning, captions `Switch` + aspect `Select` + Export MP4. (BRD-27..33, BRD-30, BRD-53, BRD-55, BRD-63) *Mockup:* docs/mockups/avatar-studio.html.
  - *Acceptance:* When a Creator changes the quality tier on Avatar studio, then the cost hint and provider badge update.
  - *Also checked:* tier change updates cost/eta hint and provider badge; Premium shows the routed-to-HeyGen banner; render shows progress + eta; safety block surfaces an `Alert`; cost estimate visible before render; visual gate at both widths.

### Page: Character library (`/avatar/characters`)

<a id="d-req-ui-011"></a>
- **REQ-UI-011** — Character grid: `Card`s with `Avatar`, realism `Badge` (photoreal/stylized), search + "New Character"; create/edit `Dialog` (image-gen prompt, realism `RadioGroup`, seed/LoRA lock); character-bible `Sheet` (TechieRag persona notes). (BRD-27, BRD-55) *Mockup:* docs/mockups/character-library.html.
  - *Acceptance:* When a Creator saves a new character on Character library, then its card appears with its realism badge.
  - *Also checked:* empty state shows create CTA; dialog saves a character with locked identity + realism flag; bible sheet opens per character; skeleton cards while loading; visual gate at both widths.

### Page: Image studio (`/image`)

<a id="d-req-ui-020"></a>
- **REQ-UI-020** — Left prompt+params panel with **dynamic controls rendered from the registry params schema**; center canvas (generate / I2I / inpaint mask `Toolbar`); ControlNet `Tabs` (pose/depth/edges) + reference `FileUpload`; upscale 2K/4K button; results strip with seed `Badge` grid; export `Select` (1:1/4:5/9:16/16:9) + bg-remove `Switch`. (BRD-16..21) *Mockup:* docs/mockups/image-studio.html.
  - *Acceptance:* When a Creator switches model on Image studio, then the parameter controls re-render from that model's schema.
  - *Also checked:* switching model re-renders params from its schema without code change; generate shows `Progress`; results show seeds and reproduce; canvas placeholder when empty; visual gate at both widths.

### Page: Voice studio (`/voice`)

<a id="d-req-ui-030"></a>
- **REQ-UI-030** — Text + language (`EN/HI/Hinglish`) controls, preset-voice `Combobox` (routing hidden behind one control), speed `Slider` + emotion `Select` (disabled when unsupported), waveform player, clone `Dialog` with `FileUpload` and a **consent `AlertDialog` gate for real people**. (BRD-22..26, BRD-62) *Mockup:* docs/mockups/voice-studio.html.
  - *Acceptance:* When a Creator generates speech on Voice studio, then a progress bar shows and then a playable clip.
  - *Also checked:* generation shows `Progress` then playable audio; real-person clone blocks until consent confirmed (synthetic/preset voices skip the gate); unsupported emotion/speed disabled; visual gate at both widths.

### Page: Video studio (`/video`)

<a id="d-req-ui-040"></a>
- **REQ-UI-040** — I2V/T2V mode `Tabs`, duration/motion `Slider`s + aspect `Select`, first/last-frame `FileUpload` conditioning, Fast/High/Premium tier selector with closed-API `Alert`, timeline/editor `Card` with trim/join/reframe `Toolbar`, interpolate `Switch` + upscale, Export MP4. (BRD-42..46, BRD-30) *Mockup:* docs/mockups/video-studio.html.
  - *Acceptance:* When a Creator switches between image-to-video and text-to-video on Video studio, then the matching controls show.
  - *Also checked:* mode tabs swap controls; tier selector behaves as in Avatar studio; timeline placeholder when empty; render progress with eta; visual gate at both widths.

### Page: Gallery (`/gallery`)

<a id="d-req-ui-050"></a>
- **REQ-UI-050** — "My Library" asset grid: filter bar (search, type/model/project `Select`s), asset `Card`s with provenance `Badge`, detail `Sheet` with full params + one-click reproduce, compare hand-off, bulk-export `Toolbar` with selection count. Per-user only — server-side owner filter (BRD-81). (BRD-47, BRD-48, BRD-49, BRD-51, BRD-81) *Mockup:* docs/mockups/gallery.html.
  - *Acceptance:* When a Creator filters by type on Gallery, then the grid shows only that user's matching assets.
  - *Also checked:* filters narrow the grid; detail sheet shows full params and reproduces; bulk export includes provenance metadata; only the signed-in user's assets appear; empty state + skeleton tiles; visual gate at both widths.

### Page: Projects (`/projects`)

<a id="d-req-ui-051"></a>
- **REQ-UI-051** — Left searchable project list (`Card`s with asset counts + dates, "New project"); right detail: project header (Open in Gallery / Rename / Archive), generation-history `DataTable` (`Tabs` All/Images/Video/Voice, per-row Reproduce), full run-parameters `Card` (prompt, character, model, tier, seed, resolution, voice, cost, provenance) with "Reproduce with these params". Per-user scoped. (BRD-48, BRD-81) *Mockup:* docs/mockups/projects.html.
  - *Acceptance:* When a Creator selects a project on Projects, then its generation history and run parameters load.
  - *Also checked:* selecting a project loads its history; per-row Reproduce carries the stored params; empty list shows create CTA; visual gate at both widths.

### Page: Comparison (`/compare`)

<a id="d-req-ui-052"></a>
- **REQ-UI-052** — Side-by-side comparison view: slot toolbar (A/B asset `Select`s, "Add third", side-by-side/overlay `Tabs`, sync-playback `Switch`); result panes with preview + model/provider·tier/seed/provenance `Badge`s and cost/duration/resolution rows; parameter-diff `DataTable` with differing rows highlighted + "N differ" badge + show-identical toggle; per-pane Reproduce/Export/Open-in-Gallery actions. No sidebar item — reached from the Gallery's Compare action (breadcrumb `Gallery / Compare`). Per-user scoped. (BRD-49, BRD-81) *Mockup:* docs/mockups/compare.html.
  - *Acceptance:* When a Creator compares two assets on Comparison, then they show side by side with differing parameters highlighted.
  - *Also checked:* two assets render side-by-side with differing params visibly highlighted; empty state (<2 assets) links back to Gallery; Reproduce re-submits the pane's stored params; visual gate at both widths.

### Page: Library (`/library`)

<a id="d-req-ui-053"></a>
- **REQ-UI-053** — Prompt/script/preset library: search + type segmented `Tabs` (All/Prompts/Scripts/Style presets/Voice presets) + "New preset" CTA; preset `Card` grid with type tag, clamped snippet or param summary, tag `Badge`s, use-count, "Use in {Studio}" + Edit actions; new/edit `Sheet`. Per-user scoped; "Use in Studio" pre-fills the owning studio's composer. (BRD-50, BRD-81) *Mockup:* docs/mockups/library.html.
  - *Acceptance:* When a Creator clicks Use in Studio on Library, then the owning studio opens with the preset applied.
  - *Also checked:* type tabs filter the grid; "Use in Studio" routes to the right studio with the preset applied; empty state shows the save-from-composer CTA; visual gate at both widths.

### Page: Account & spend (`/account`)

<a id="d-req-ui-060"></a>
- **REQ-UI-060** — Profile card (AppManager identity), 3 spend KPI cards with `Progress` (RunPod / closed-API / total vs monthly cap, danger when over), license/tier card from LicenseSvc with upgrade CTA when gated, recent-usage `DataTable`. (BRD-52, BRD-58, BRD-59, BRD-75) *Mockup:* docs/mockups/account.html.
  - *Acceptance:* When a Creator opens Account, then spend shows against the monthly cap with warning and danger states.
  - *Also checked:* spend renders against the cap with warning/danger states; license/tier reads from AppManager; empty usage message; skeleton KPIs while loading; visual gate at both widths.

### Page: Admin — Hardware & guided setup (`/admin/setup`, `/admin/setup/hardware`)

<a id="d-req-ui-070"></a>
- **REQ-UI-070** — Readiness summary cards (GPU cores / unified memory / MPS-CoreML / disk) with ready/warn `Badge`s, guided-setup stepper (ONNX install, isolated Python+ComfyUI provisioning) with `Progress`, low-disk `Alert` linking installs to the Model Library screen. (BRD-8, BRD-9, BRD-10) *Mockup:* docs/mockups/admin-hardware.html.
  - *Acceptance:* When an Operator opens Hardware and guided setup, then readiness cards show this machine's capabilities and setup steps.
  - *Also checked:* detection shows skeletons then per-capability status; setup steps show progress and surface failures as `Alert`s; disk warning links to `/admin/models`; visual gate at both widths.

### Page: Admin — Registry & providers (`/admin/registry`, `/admin/providers`)

<a id="d-req-ui-071"></a>
- **REQ-UI-071** — Registry `DataTable` (id, displayName, task, providerType, tier `Badge`, license, safetyClass; filter/sort); add/edit `Sheet` with the declarative params-schema editor (key/type/required/default rows, inline validation); Provider-keys panel (HeyGen/aggregator `Input` Password + enabled `Switch`, masked, never logged) — `/admin/providers` renders as this panel; no separate screen. (BRD-6, BRD-7, BRD-77) *Mockup:* docs/mockups/admin-registry.html.
  - *Acceptance:* When an Operator edits and saves a registry row on Registry and providers, then the change persists after reload.
  - *Also checked:* row CRUD round-trips incl. params schema; keys masked and stored encrypted; empty registry shows `Empty` CTA; visual gate at both widths.

### Page: Admin — ComfyUI import & debug (`/admin/comfy/import`, `/admin/comfy/debug`)

<a id="d-req-ui-072"></a>
- **REQ-UI-072** — Workflow drop-zone `FileUpload` + paste `Textarea` (parse errors → `Alert`); parsed nodes/inputs `DataTable`; input→registry-param mapping rows; run/debug panel with per-node status, exact JSON sent, WebSocket previews and `Progress`; missing-deps `Alert` with Install action; "Save as model" + version `Select` + known-good `Badge`. (BRD-34..41) *Mockup:* docs/mockups/admin-comfy.html.
  - *Acceptance:* When an Operator pastes invalid workflow JSON on ComfyUI import, then a parse error alert shows.
  - *Also checked:* invalid JSON surfaces a parse `Alert`; mapping edits persist; run streams per-node status + previews; save-as-model + pin round-trips; visual gate at both widths.

### Page: Admin — RunPod & ops (`/admin/ops`, `/admin/log`)

<a id="d-req-ui-073"></a>
- **REQ-UI-073** — Spend/guardrail cards with `Progress` (RunPod / closed-API / cap, near-cap danger + `Alert`); RunPod control card (start/stop `Button`, auto-idle-stop `Switch`, cold-start `Badge`); worker health + queue depth cards; filterable generation-log `DataTable` with reproduce action. (BRD-12, BRD-13, BRD-14, BRD-15, BRD-75) *Mockup:* docs/mockups/admin-ops.html.
  - *Acceptance:* When an Operator opens RunPod and ops, then spend cards, worker health and the generation log show live data.
  - *Also checked:* spend renders against the cap incl. closed APIs; endpoint state reflects scale-to-zero transitions; log filters and reproduces; empty log message + skeleton KPIs; visual gate at both widths.

### Page: Admin — Model sources (`/admin/sources`)

<a id="d-req-ui-074"></a>
- **REQ-UI-074** — Sources list (name, type HuggingFace/Civitai/Custom, base URL, enabled `Switch`, status `Badge`) with header search + "＋ Add source" opening the add/edit `Sheet` (name, type, base URL, API token Password, enabled); Test-connection button (ok `Badge` / fail `Alert`); info `Alert` distinguishing sources (open model files) from Providers (closed generation keys). (BRD-82) *Mockup:* docs/mockups/admin-sources.html.
  - *Acceptance:* When an Operator adds a source on Model sources, then it appears in the list with its token masked.
  - *Also checked:* add/edit/enable round-trips; tokens stored encrypted, never logged; test-connection reports reachability; empty state CTA; visual gate at both widths.

### Page: Admin — Content policy / NSFW (`/admin/policy`)

<a id="d-req-ui-075"></a>
- **REQ-UI-075** — Single content-policy card: "Unrestricted (NSFW) mode" `Switch` (off by default) behind a confirmation `AlertDialog` ("synthetic adult 18+ personas only"); always-locked floors list (CSAM · real-person/NCII · minors · public-figure · provenance · audit — "cannot be disabled"); scope note; toggle disabled with explanation in multi-user/SaaS mode. (BRD-79, BRD-80) *Mockup:* docs/mockups/admin-policy.html.
  - *Acceptance:* When the Owner turns on unrestricted mode on Content policy, then a confirmation dialog must be accepted first.
  - *Also checked:* enabling requires explicit confirmation; floors list always visible; toggle disabled + note when multi-user; visual gate at both widths.

### Page: Admin — Model library (`/admin/models`)

<a id="d-req-ui-076"></a>
- **REQ-UI-076** — Search + task `Tabs` + source `Select` over a results `DataTable` (name, source, task, size, min VRAM, license badge — restrictive → warn) with one-click Install; footer `Pagination`; right rail: resumable install queue (`Progress`, Pause/Resume/Cancel, speed + destination) and destination card (local vs RunPod network volume, >10 GB defaults to volume, over-disk `Alert`); registry hand-off info `Alert`. Browses only ENABLED sources. (BRD-11, BRD-82) *Mockup:* docs/mockups/admin-models.html.
  - *Acceptance:* When an Operator installs a model on Model library, then the install queue shows its progress with pause and resume.
  - *Also checked:* no-sources empty state links to `/admin/sources`; install shows per-row progress and is pausable/resumable; license + VRAM shown before install; pagination works; visual gate at both widths.

### Page: Unified role-based app shell

<a id="d-req-ui-080"></a>
- **REQ-UI-080** — Merge the `TrStudioAdmin` management screens into the ONE `TrStudio` app on the shared `TrStudioUI` RCL; role-gated navigation presents the Creator studios, the Management surface, or both, by the signed-in user's role; retire the separate `TrStudioAdmin` host. Supersedes the two-app split (BRD-86, ADR-012). *Amended 2026-07-05.* *Mockup:* docs/mockups/admin-ops.html.
  - *Acceptance:* When a Creator signs in to the app, then only the creator studios appear in the sidebar.
  - *Also checked:* the former `TrStudioAdmin` management screens render inside the single `TrStudio` app from `TrStudioUI` (no separate admin host).
  - *Also checked:* sidebar/navigation is role-gated — Creator sees only creator studios, Operator/Admin sees only management, Owner sees both with an in-app switch.
  - *Also checked:* the retired `TrStudioAdmin` host is no longer required to reach any management screen; visual gate at both widths.

### Page: Prompt studio (`/prompt-studio`)

<a id="d-req-ui-081"></a>
- **REQ-UI-081** — Prompt-crafting chat screen: message thread + composer, a registry-driven **text-chat LLM picker** (`Select`/`Combobox` over registry rows with `task: "text-chat"`), and an **image attachment** control (e.g. a photo to be edited) that is enabled only when the picked LLM is vision-capable (otherwise disabled with a hint to pick a vision model). Assistant replies render the crafted prompt in a copyable block. Listed with the creator studios in the nav. (BRD-88, BRD-89, BRD-90) *Mockup:* docs/mockups/prompt-studio.html. *Added 2026-07-07.*
  - *Acceptance:* When a Creator picks a text-only model on Prompt studio, then the image attach control is disabled with a hint.
  - *Also checked:* the LLM picker lists only registry `text-chat` rows; switching models toggles the attachment control per `visionCapable`; sending a message with the API down shows a clean `Alert` (no dead UI); the crafted-prompt block has a working Copy action; visual gate at desktop + mobile widths.

<a id="d-req-ui-082"></a>
- **REQ-UI-082** — Prompt-studio session rail + prompt-use actions: a per-user session list (new / resume / rename / delete — owner-scoped, BRD-81) alongside the chat, and per-reply actions **Copy**, **Save to library** (creates a `prompt` preset per BRD-50), and **Send to studio** (routes the prompt into the Image / Avatar / Video studio prompt field). (BRD-91, BRD-92) *Mockup:* docs/mockups/prompt-studio.html. *Added 2026-07-07.*
  - *Acceptance:* When a Creator reloads the page on Prompt studio, then their saved chat sessions list and resume with full history.
  - *Also checked:* sessions persist across reload and list only the signed-in user's sessions; resume restores the full message history; Save-to-library lands the prompt in `/library` as a `prompt` preset; Send-to-studio opens the chosen studio with the prompt pre-filled; visual gate at both widths.

## Functional requirements

<!-- Built by the unified build-phase (flow-master). Grouped by BRD ledger phase. -->

### Phase 1 — Platform spine, registry, identity

<a id="d-req-fn-001"></a>
- **REQ-FN-001** — `IGenerationProvider` (supports/submit/status/result/estimateCost) defined in `TrStudio.Models`; implementations in `TrStudio.Engine/Providers/`; orchestrator resolves model → exactly one provider so the UI never knows which ran. *Also checked:* a stub provider registered against a registry row runs a job end-to-end through the abstraction. (BRD-1)
  - *Acceptance:* When a stub-model job is submitted from Image studio, then it runs end to end and stores an asset.

<a id="d-req-fn-002"></a>
- **REQ-FN-002** — Async job model: submit → poll/stream → fetch, identical across providers via `/api/jobs`. *Also checked:* the same endpoint sequence drives two different providers with no caller-visible difference. (BRD-2)
  - *Acceptance:* When a client submits and polls on the jobs API, then the status sequence is identical for any provider.

<a id="d-req-fn-003"></a>
- **REQ-FN-003** — Durable Hangfire/Postgres queue: priority, concurrency caps, retries, back-pressure; no Redis. *Also checked:* an enqueued job survives an API restart and completes; a failing job retries per policy. (BRD-3)
  - *Acceptance:* When the API restarts with a job queued on the jobs API, then the job still completes afterwards.

<a id="d-req-fn-004"></a>
- **REQ-FN-004** — Unified `TrStudioApi` surface: OpenAPI-described (`/swagger`), authenticated, versioned; admin-scoped endpoints separated; DbUp (`TrStudioDb`) runs on startup. *Also checked:* swagger lists versioned, auth-protected endpoints; schema migrates on boot. (BRD-4)
  - *Acceptance:* When an unauthenticated caller requests a versioned route on the API, then it is refused with 401.

<a id="d-req-fn-005"></a>
- **REQ-FN-005** — SignalR progress hub streams job progress/eta; failures are actionable (e.g. "out of memory — drop to 256px or route to RunPod"). *Also checked:* a running job pushes progress events; a provoked failure carries a remediation hint. (BRD-5)
  - *Acceptance:* When a job runs from a studio, then progress events stream to the screen until it succeeds.

<a id="d-req-fn-006"></a>
- **REQ-FN-006** — Model & Provider Registry as single source of truth (task, providerType, tier, hardware, languages, license, safetyClass) with a declarative params schema that auto-renders UI controls. *Also checked:* adding a registry row makes the model selectable with rendered params, no code change. (BRD-6)
  - *Acceptance:* When an Operator adds a registry row on Registry and providers, then the model is selectable in its studio.

<a id="d-req-fn-007"></a>
- **REQ-FN-007** — Closed-provider adapters (HeyGen, aggregator) configured with encrypted API-key storage, enabled per model. *Also checked:* keys round-trip encrypted at rest, masked in UI/logs; disabling a provider hides its models. (BRD-7)
  - *Acceptance:* When an Operator saves a provider key on Registry and providers, then it is stored encrypted and shown masked.

<a id="d-req-fn-008"></a>
- **REQ-FN-008** — Host capability detection: Apple Silicon GPU cores, unified memory, MPS/CoreML, disk; readiness summary. *Also checked:* detection returns a structured readiness report consumed by REQ-UI-070. (BRD-8)
  - *Acceptance:* When an Operator opens Hardware and guided setup, then a readiness report of this machine's capabilities shows.

<a id="d-req-fn-009"></a>
- **REQ-FN-009** — Guided setup installs ONNX Runtime and provisions the managed, isolated Python env + ComfyUI with no terminal use. *Also checked:* setup completes from the UI on a bare host; failures surface actionable errors. (BRD-9)
  - *Acceptance:* When an Operator enters a running ComfyUI's address on Hardware and guided setup and presses Connect, then it shows "Connected to ComfyUI" with its version and keeps the address.
  - *Changed 2026-11-19 (owner decision 17B):* was "ONNX Runtime and ComfyUI install without a terminal"; on Mac the App Sandbox stays and ComfyUI runs on its own (ADR-029).

<a id="d-req-fn-010"></a>
- **REQ-FN-010** — Isolated Python/ComfyUI environment lifecycle managed by the engine; never collides with system Python. *Also checked:* provisioning/upgrading touches only the managed env path. (BRD-10)
  - *Acceptance:* When an Operator opens Hardware and guided setup on the Mac app, then no Python step is offered and connecting ComfyUI starts no program.
  - *Changed 2026-11-19 (owner decision 17B):* was "only the managed environment path changes" for every host (ADR-029).

<a id="d-req-fn-011"></a>
- **REQ-FN-011** — Model install service: one-click install/import of curated open models, resumable downloads, size/VRAM/licence surfaced, big media models stored on the RunPod network volume. *Also checked:* an interrupted download resumes; >10 GB models default to the volume. (BRD-11)
  - *Acceptance:* When a model download is interrupted on Model library, then resuming continues from where it stopped.

<a id="d-req-fn-012"></a>
- **REQ-FN-012** — AppManager login integration: child-app handshake (`X-Api-Key`/`X-Api-Secret`), JWT + refresh, RSA-encrypted passwords, no local password store. *Also checked:* login yields a session JWT via AppManager; no password persisted locally. (BRD-57)
  - *Acceptance:* When a user signs in on Login, then AppManager issues the session token and no password is stored locally.

<a id="d-req-fn-013"></a>
- **REQ-FN-013** — License/subscription validation via AppManager LicenseSvc. *Also checked:* an expired/absent license is reported and gates accordingly. (BRD-58)
  - *Acceptance:* When a user opens Account, then the licence status read from AppManager is shown.

<a id="d-req-fn-014"></a>
- **REQ-FN-014** — Feature/tier gating via AppManager FeatureSvc (e.g. Premium behind a paid license in multi-user mode; personal mode = all tiers). *Also checked:* a gated tier is refused server-side with an upgrade-prompt response. (BRD-59)
  - *Acceptance:* When a user without a Premium licence picks Premium on Avatar studio, then the server refuses with an upgrade prompt.

<a id="d-req-fn-015"></a>
- **REQ-FN-015** — Registration via the AppManager child-app API; generic superadmin (`admin@appmanager.local`) bootstraps the first session; password reset requested via AppManager. *Also checked:* a new account created from `/register` can sign in; superadmin works on first run. (BRD-83)
  - *Acceptance:* When a visitor registers on Register, then they can sign in with the new account.

<a id="d-req-fn-016"></a>
- **REQ-FN-016** — Role-based default landing after login per the BRD §9 F-IDENTITY table (Owner/Creator → `/avatar`; Operator/Admin → `/admin/ops`; setup-incomplete variants per BRD-85). *Also checked:* each role lands on its table-specified page; nobody lands on a blank screen. (BRD-84)
  - *Acceptance:* When an Operator signs in on Login, then they land on RunPod and ops.

<a id="d-req-fn-017"></a>
- **REQ-FN-017** — First-run setup gate on every login: complete = ≥1 enabled model source AND ≥1 ready registry model; admins route into setup (hardware → guided → sources → install), Creators to `/setup-required`; no generation UI until the gate passes. *Also checked:* gate outcomes route each role correctly and flip once setup completes. (BRD-85)
  - *Acceptance:* When a Creator signs in before setup completes, then they are sent to Setup required instead of a studio.

<a id="d-req-fn-018"></a>
- **REQ-FN-018** — Generation log records who/model/params/cost/duration/outcome for every job — history, one-click reproduce, audit. *Also checked:* every job (success or failure) writes a complete log row; reproduce re-submits from it. (BRD-14)
  - *Acceptance:* When any job finishes from a studio, then a generation log row records user, model, cost, duration and outcome.

### Phase 2 — Local image vertical + workspace foundation

<a id="d-req-fn-019"></a>
- **REQ-FN-019** — Text-to-image via `OnnxLocalProvider` (CoreML/CPU EP) driven by the registry params schema. *Also checked:* a prompt renders an image locally; params honor the schema. (BRD-16)
  - *Acceptance:* When a Creator generates from a prompt on Image studio with a local model, then an image renders locally.

<a id="d-req-fn-020"></a>
- **REQ-FN-020** — Image-to-image, inpaint/outpaint and mask-based edits. *Also checked:* a masked region regenerates while the rest is preserved. (BRD-17)
  - *Acceptance:* When a Creator inpaints a masked region on Image studio, then only that region changes.

<a id="d-req-fn-021"></a>
- **REQ-FN-021** — Reference-image / ControlNet conditioning (pose/depth/edges) + face/identity lock for characters. *Also checked:* conditioning measurably constrains output; identity lock reuses the character's reference set/seed. (BRD-18)
  - *Acceptance:* When a Creator adds a pose reference on Image studio, then the output follows the reference pose.

<a id="d-req-fn-022"></a>
- **REQ-FN-022** — Upscale + face restoration to 2K/4K. *Also checked:* output at requested resolution with face restoration applied. (BRD-19)
  - *Acceptance:* When a Creator upscales an image to 4K on Image studio, then the result has the requested resolution.

<a id="d-req-fn-023"></a>
- **REQ-FN-023** — Batch/grid variations with seed control + reproducibility. *Also checked:* same seed + params reproduce an identical image; a batch yields the requested grid. (BRD-20)
  - *Acceptance:* When a Creator reruns the same seed and parameters on Image studio, then an identical image is produced.

<a id="d-req-fn-024"></a>
- **REQ-FN-024** — Social-format export presets (1:1, 4:5, 9:16, 16:9) + background removal. *Also checked:* exports land at preset aspect ratios; bg-removal produces transparency. (BRD-21)
  - *Acceptance:* When a Creator exports with the 9:16 preset on Image studio, then the file has a 9:16 aspect ratio.

<a id="d-req-fn-025"></a>
- **REQ-FN-025** — Asset gallery backend: every output browsable/filterable (type/model/project) and reusable as an input. *Also checked:* filters work server-side; an asset can seed a new job. (BRD-47)
  - *Acceptance:* When a Creator filters by model on Gallery, then the server returns only matching assets.

<a id="d-req-fn-026"></a>
- **REQ-FN-026** — Projects/folders with full-parameter history and one-click reproduce. *Also checked:* every run stores its complete parameter set; reproduce re-runs it byte-for-byte (same seed). (BRD-48)
  - *Acceptance:* When a Creator clicks Reproduce on a run in Projects, then the job reruns with the stored parameters and seed.

<a id="d-req-fn-027"></a>
- **REQ-FN-027** — Per-user isolation: every character/job/asset scoped to its AppManager `userId`; the owner filter applied to **every** engine data-access query (Dapper, parameterized) — server-side, not just UI. *Also checked:* user A can never read/list/reproduce user B's entities via API or UI. (BRD-81)
  - *Acceptance:* When user A requests user B's asset by id on the API, then the request returns not found.

### Phase 3 — Managed ComfyUI + RunPod

<a id="d-req-fn-028"></a>
- **REQ-FN-028** — RunPod serverless integration: register endpoint/template, submit jobs, stream status, scale to zero automatically (start/stop on demand). *Also checked:* a heavy job cold-starts a worker, runs, and the worker idle-stops to zero. (BRD-12)
  - *Acceptance:* When a High-tier job is submitted on Avatar studio, then a RunPod worker starts, runs it, then stops.

<a id="d-req-fn-029"></a>
- **REQ-FN-029** — Cost guardrails: per-job estimate before run, RunPod idle auto-stop, monthly spend cap with alerts across RunPod **and** closed APIs (default $100). *Also checked:* an over-cap job is warned/blocked; spend accrues across both channels. (BRD-13)
  - *Acceptance:* When a job would exceed the monthly cap on Avatar studio, then it is blocked with an over-cap message.

<a id="d-req-fn-030"></a>
- **REQ-FN-030** — Model-repository sources: CRUD for Hugging Face / Civitai / custom URL registries (base URL, optional encrypted token, enabled flag); the model library browses/installs from enabled sources only. Distinct from provider generation keys. *Also checked:* enabling a source makes its models browsable; tokens never logged. (BRD-82)
  - *Acceptance:* When an Operator enables a source on Model sources, then its models become browsable on Model library.

### Phase 4 — Voice (EN + HI)

<a id="d-req-fn-031"></a>
- **REQ-FN-031** — TTS in English, Hindi and Hinglish with a preset-voice library. *Also checked:* the same text synthesizes in EN and HI with selectable preset voices. (BRD-22)
  - *Acceptance:* When a Creator synthesizes the same text in English and Hindi on Voice studio, then both clips play.

<a id="d-req-fn-032"></a>
- **REQ-FN-032** — Voice cloning from a short sample; consent-gated for real people (synthetic/preset voices ungated) — gate wired to REQ-FN-058 evidence. *Also checked:* real-person clone refuses without recorded consent; synthetic clone proceeds. (BRD-23)
  - *Acceptance:* When a Creator clones a real person's voice on Voice studio without consent, then the clone is refused.

<a id="d-req-fn-033"></a>
- **REQ-FN-033** — Per-language voice model routing (IndicF5/XTTS-Hindi for HI; XTTS/OmniVoice for EN) hidden behind one "voice" control. *Also checked:* language choice transparently selects the routed model. (BRD-24)
  - *Acceptance:* When a Creator picks Hindi on Voice studio, then the job runs on the Hindi voice model.

<a id="d-req-fn-034"></a>
- **REQ-FN-034** — Emotion/style and speed controls where the model supports them. *Also checked:* supported controls alter output; unsupported ones are reported/disabled. (BRD-25)
  - *Acceptance:* When a Creator picks a voice without emotion support on Voice studio, then the emotion control is disabled.

<a id="d-req-fn-035"></a>
- **REQ-FN-035** — Automatic alignment of generated audio to the avatar pipeline (timing + lip-sync handoff). *Also checked:* voice output hands off to an avatar job without manual timing work. (BRD-26)
  - *Acceptance:* When a Creator sends a clip on Voice studio to the avatar, then the avatar job receives its timing.

### Phase 5 — Avatar pipeline (lead feature)

<a id="d-req-fn-036"></a>
- **REQ-FN-036** — Character as a first-class entity: synthetic persona (photoreal or stylized), locked identity (reference set + seed/embedding, optional LoRA), realism flag scoping model choice and safety class; reusable everywhere. *Also checked:* a saved character reuses its locked identity across studios; realism flag persists. (BRD-27)
  - *Acceptance:* When a Creator picks a saved character on Avatar studio, then its locked identity and realism flag are reused.

<a id="d-req-fn-037"></a>
- **REQ-FN-037** — Talking avatar from character image + script/audio with lip-sync, routed to MuseTalk/LatentSync/SadTalker/LivePortrait, a RunPod heavy model, or HeyGen per the quality setting. *Also checked:* one character+audio input yields a lip-synced clip on at least the Fast route. (BRD-28)
  - *Acceptance:* When a Creator renders a character with audio on Avatar studio at Fast tier, then a lip-synced clip is produced.

<a id="d-req-fn-038"></a>
- **REQ-FN-038** — Script→video end-to-end: type a script (EN/HI), pick character + voice, get a narrated clip. *Also checked:* the full chain (script → TTS → lip-sync → clip) runs as one pipeline. (BRD-29)
  - *Acceptance:* When a Creator renders a script with a character and voice on Avatar studio, then one narrated clip is produced.

<a id="d-req-fn-039"></a>
- **REQ-FN-039** — Quality/cost tier routing: Fast (Mac local) / High (RunPod) / Premium (HeyGen) selected per job on one control; the abstraction swaps the provider underneath. *Also checked:* the same job spec routes to a different provider per tier with no other change. (BRD-30)
  - *Acceptance:* When a Creator changes only the tier on Avatar studio, then the same job routes to a different provider.

<a id="d-req-fn-040"></a>
- **REQ-FN-040** — Captions/subtitles (from the script or Whisper) in EN/HI, burn-in or sidecar SRT (text via REQ-RAG-002). *Also checked:* an export carries burned captions or an SRT matching the audio. (BRD-31)
  - *Acceptance:* When a Creator exports with captions on Avatar studio, then the clip carries captions matching the audio.

The digital twin (BRD-32) and multi-shot assembly (BRD-33) moved to phase 3 on 17 Nov 2026 with their rows; see [the phase 3 checklist](TrStudio-P3-Checklist.md).

### Phase 6 — ComfyUI pipeline management

<a id="d-req-fn-043"></a>
- **REQ-FN-043** — Import a ComfyUI workflow (drag-drop API-format JSON or paste), parse nodes, list parameterizable inputs. *Also checked:* a valid workflow parses to a node/input list; invalid JSON reports a parse error. (BRD-34)
  - *Acceptance:* When an Operator drops a valid workflow on ComfyUI import, then its nodes and inputs are listed.

<a id="d-req-fn-044"></a>
- **REQ-FN-044** — Map workflow inputs → registry params (required, types, defaults, hide internal nodes) for auto-rendered controls. *Also checked:* a mapped workflow exposes exactly the mapped params in the registry schema. (BRD-35)
  - *Acceptance:* When an Operator maps workflow inputs on ComfyUI import, then exactly those inputs become the model's parameters.

<a id="d-req-fn-045"></a>
- **REQ-FN-045** — Run an imported workflow against a chosen worker (local MPS or RunPod), streaming progress + previews via WebSocket. *Also checked:* a run streams live progress and preview frames to the debug view. (BRD-36)
  - *Acceptance:* When an Operator runs a workflow on ComfyUI import against a worker, then progress and previews stream live.

<a id="d-req-fn-046"></a>
- **REQ-FN-046** — Debug view data: per-node status, error messages, the exact JSON sent, missing-model/custom-node detection with install hints. *Also checked:* a failing node surfaces its error + the exact submitted JSON. (BRD-37)
  - *Acceptance:* When a workflow node fails on ComfyUI debug, then its error and the exact JSON sent are shown.

<a id="d-req-fn-047"></a>
- **REQ-FN-047** — Detect and (where possible) auto-install required custom nodes + model files for an imported workflow. *Also checked:* a workflow with a missing node reports it and installs it when available. (BRD-38)
  - *Acceptance:* When an Operator imports a workflow missing a custom node on ComfyUI import, then the node is reported and installed.

<a id="d-req-fn-048"></a>
- **REQ-FN-048** — Save an imported+mapped workflow as a registered model usable from `TrStudio` (graph hidden from end users). *Also checked:* the saved model appears in the registry and renders params in the creator app with no graph exposure. (BRD-39)
  - *Acceptance:* When an Operator saves a mapped workflow on ComfyUI import, then Creators can use it without seeing the graph.

<a id="d-req-fn-049"></a>
- **REQ-FN-049** — Version workflows and pin a known-good version per registered model. *Also checked:* a model keeps serving its pinned version while newer versions exist. (BRD-40)
  - *Acceptance:* When a newer workflow version is saved on ComfyUI import, then the model keeps serving its pinned version.

<a id="d-req-fn-050"></a>
- **REQ-FN-050** — Sandbox/validate a workflow with a smoke test before it goes live. *Also checked:* a workflow cannot be registered live without a passing smoke run. (BRD-41)
  - *Acceptance:* When a workflow fails its smoke run on ComfyUI import, then it cannot be registered live.

### Phase 7 — Video + closed API

<a id="d-req-fn-051"></a>
- **REQ-FN-051** — Image-to-video and text-to-video (open model on RunPod, or closed API via aggregator) with duration/motion/aspect controls. *Also checked:* both modes produce clips honoring the controls. (BRD-42)
  - *Acceptance:* When a Creator generates text-to-video on Video studio, then the clip honours the chosen duration and aspect.

<a id="d-req-fn-052"></a>
- **REQ-FN-052** — First/last-frame + reference-image conditioning for consistency. *Also checked:* conditioned generation matches the pinned frames/reference. (BRD-43)
  - *Acceptance:* When a Creator pins first and last frames on Video studio, then the clip starts and ends on them.

<a id="d-req-fn-053"></a>
- **REQ-FN-053** — Frame interpolation (RIFE/FILM) and video upscale as post-steps. *Also checked:* post-steps raise fps/resolution on an existing clip. (BRD-44)
  - *Acceptance:* When a Creator applies interpolation to a clip on Video studio, then the clip's frame rate increases.

<a id="d-req-fn-054"></a>
- **REQ-FN-054** — Trim/join/reframe, add audio (from Voice studio), export MP4 in platform formats. *Also checked:* an edited clip with added audio exports as MP4 at the chosen format. (BRD-45)
  - *Acceptance:* When a Creator adds audio and exports on Video studio, then an MP4 in the chosen format downloads.

Making a clip longer (BRD-46) moved to phase 3 on 17 Nov 2026 with its row; see [the phase 3 checklist](TrStudio-P3-Checklist.md).

### Phase 8 — Safety, provenance & NSFW mode (wired from Phase 2 onward)

> SAFE-01..06 are **non-disableable floors** — enforced on every provider regardless of any mode, including Unrestricted (F-NSFW). Wired incrementally from Phase 2; Phase 8 is the consolidation/verification pass.

<a id="d-req-fn-056"></a>
- **REQ-FN-056** — CSAM prevention by design: hash-matching + classifier on inputs AND outputs, hard block + log, on every provider; never toggleable. *Also checked:* a flagged input/output is blocked and audit-logged on every provider path. (BRD-60, SAFE-01)
  - *Acceptance:* When a flagged input is submitted from any studio, then it is blocked and written to the audit log.

<a id="d-req-fn-057"></a>
- **REQ-FN-057** — Block sexualisation/face-swap of real, identifiable people; refuse NCII/sexual deepfakes. *Also checked:* a real-person sexual job is refused and logged regardless of mode. (BRD-61, SAFE-02)
  - *Acceptance:* When a sexual job of a real person is submitted from any studio, then it is refused and logged.

<a id="d-req-fn-058"></a>
- **REQ-FN-058** — Recorded consent step (retained as evidence) required for any real-person face or voice clone. *Also checked:* real-person clone jobs are blocked until consent evidence exists; evidence is retained and auditable. (BRD-62, SAFE-03)
  - *Acceptance:* When a real-person face clone is submitted from Character library without consent evidence, then it is blocked.

<a id="d-req-fn-059"></a>
- **REQ-FN-059** — Provenance/AI-disclosure metadata (C2PA-style, non-trivial to strip; respect provider watermarks) embedded on every output. *Also checked:* every export carries provenance metadata; provider watermarks pass through. (BRD-63, SAFE-04)
  - *Acceptance:* When a Creator exports any asset from Gallery, then the file carries provenance metadata.

<a id="d-req-fn-060"></a>
- **REQ-FN-060** — Public-figure impersonation blocked by default; allowed only with explicit recorded rights. *Also checked:* a recognizable-public-figure job is refused without a rights record. (BRD-64, SAFE-05)
  - *Acceptance:* When a public-figure job is submitted from any studio without a rights record, then it is refused.

<a id="d-req-fn-061"></a>
- **REQ-FN-061** — Per-deployment policy config, abuse reporting, and a full generation audit trail. *Also checked:* policy loads per deployment; abuse reports persist; every generation is traceable in the audit trail. (BRD-65, SAFE-06)
  - *Acceptance:* When a Creator submits an abuse report from Gallery, then it persists in the audit trail.

<a id="d-req-fn-062"></a>
- **REQ-FN-062** — Owner-only Unrestricted (NSFW) mode: lifts the general SFW filter for fully-synthetic adult (18+) personas; admin setting, off by default, personal/single-user mode only, auto-disabled in multi-user/SaaS via AppManager FeatureSvc. *Also checked:* toggle works only in personal mode; multi-user forces SFW-locked regardless of the stored setting. (BRD-79)
  - *Acceptance:* When multi-user mode is active on Content policy, then the unrestricted mode toggle is forced off.

<a id="d-req-fn-063"></a>
- **REQ-FN-063** — NSFW scope enforcement: applies only to characters flagged synthetic + adult; refuses photoreal-of-real-person, real-person references and minors; SAFE-01..06 floors (REQ-FN-056..061) remain enforced with the mode ON. *Also checked:* with mode ON, a real-person or minor-flagged job is still refused + logged; floors verifiably fire. (BRD-80)
  - *Acceptance:* When a minor-flagged job is submitted from Avatar studio with unrestricted mode on, then it is still refused and logged.

### Phase 9 — Polish + SaaS-ready seams

<a id="d-req-fn-064"></a>
- **REQ-FN-064** — Ops dashboard backend: worker health, queue depth, spend-to-date, error rates. *Also checked:* the ops endpoints report live health/queue/spend/error data consumed by REQ-UI-073. (BRD-15)
  - *Acceptance:* When an Operator opens RunPod and ops, then live worker health, queue depth and spend show.

<a id="d-req-fn-065"></a>
- **REQ-FN-065** — Side-by-side comparison of outputs across models/seeds/providers. *Also checked:* two+ assets render side-by-side with their differing params highlighted. *(UI: REQ-UI-052 / docs/mockups/compare.html.)* (BRD-49)
  - *Acceptance:* When a Creator opens two assets in Comparison, then their differing parameters are highlighted.

<a id="d-req-fn-066"></a>
- **REQ-FN-066** — Prompt + script library with reusable style/voice presets. *Also checked:* a saved prompt/preset is reusable from the studios. *(UI: REQ-UI-053 / docs/mockups/library.html.)* (BRD-50)
  - *Acceptance:* When a Creator saves a prompt preset from Image studio, then it is reusable from Library.

<a id="d-req-fn-067"></a>
- **REQ-FN-067** — Export/download single + bulk, with provenance metadata included. *Also checked:* bulk export delivers all selected assets with provenance intact. (BRD-51)
  - *Acceptance:* When a Creator bulk-exports selected assets on Gallery, then every file arrives with its provenance.

<a id="d-req-fn-068"></a>
- **REQ-FN-068** — Account usage/spend view backend with license/subscription & billing surfaced from AppManager. *Also checked:* spend aggregates per user; license state reads live from AppManager. (BRD-52)
  - *Acceptance:* When a Creator opens Account, then spend is totalled per user and the licence reads live from AppManager.

<a id="d-req-fn-069"></a>
- **REQ-FN-069** — `TrStudioApi` designed to serve as an external REST API later: API-key auth seam, versioned contracts, no UI-only capabilities. *Also checked:* a job submits end-to-end with API-key auth (no interactive session). (BRD-66)
  - *Acceptance:* When a client submits a job with an API key on the API, then it completes without an interactive session.

<a id="d-req-fn-070"></a>
- **REQ-FN-070** — Webhooks fire on job completion/failure. **Deferred (Could).** *Also checked:* a registered webhook receives completion and failure payloads. (BRD-67)
  - *Acceptance:* When a job completes from a studio, then the registered webhook receives the completion payload.

<a id="d-req-fn-071"></a>
- **REQ-FN-071** — MCP server exposing generation as agent tools. **Deferred (Could).** *Also checked:* an MCP client lists and invokes a generation tool. (BRD-68)
  - *Acceptance:* When an MCP client connects on the API's MCP endpoint, then it lists and invokes a generation tool.

### P-amend — Unified role-based app (2026-07-05)

<a id="d-req-fn-072"></a>
- **REQ-FN-072** — Role-based surface authorization & routing within the single `TrStudio` app: after login, present AND authorize the creator vs management areas by role — server-side (endpoint/authorization policy) and client (navigation/routing) — with the admin-only host retired. Owner sees both surfaces with an in-app switch. (BRD-86) *Amended 2026-07-05.*
  - *Acceptance:* When a Creator calls a management endpoint on the API, then the server refuses with 403.
  - *Also checked:* a Creator is routed to the studios and is refused management routes/endpoints server-side; an Operator/Admin is routed to management; an Owner can access both and switch in-app.
  - *Also checked:* authorization is enforced server-side (not only hidden in the UI) and the separate admin host is no longer needed.

### Phase 10 — Prompt Studio (added 2026-07-07)

<a id="d-req-fn-073"></a>
- **REQ-FN-073** — Prompt-chat persistence + endpoints: `PromptChatSession` / `PromptChatMessage` tables (DbUp script in `TrStudioDb`), Dapper repository in `TrStudio.Engine/Infrastructure/` with the BRD-81 owner filter on every query, and `prompt-chat` endpoints in `TrStudioApi` (create/list/rename/delete session; post message → assistant reply). No Hangfire job — synchronous chat path outside the generation spine (ADR-013). (BRD-91) *Added 2026-07-07.*
  - *Acceptance:* When another user requests a Prompt studio session by id on the API, then it returns not found.
  - *Also checked:* sessions/messages persist and reload; a foreign user's session returns 404; deleting a session removes its messages; endpoints require auth.

<a id="d-req-fn-074"></a>
- **REQ-FN-074** — Text-chat LLMs as registry rows: `task: "text-chat"` / `providerType: "textllm"` entries (displayName, endpoint, encrypted API key via the existing key store, `visionCapable`, token cost) manageable on `/admin/registry`; excluded from `IGenerationProvider` resolution and served to the Prompt-studio picker via the models API. (BRD-89) *Added 2026-07-07.*
  - *Acceptance:* When an Operator adds a text-chat row on Registry and providers, then it appears in the Prompt studio model picker.
  - *Also checked:* adding/editing a text-chat row is config-only (no code change, NFR-09); keys are masked at rest; the picker endpoint returns only `text-chat` rows; generation-job submission against a text-chat model is rejected.

## RAG / AI requirements (→ /techierag)

<!-- Built by /techierag as a sub-agent of build-phase. TechieRag is text/LLM/RAG ONLY —
     never the image/video path. PgVector store lives on the same Postgres instance. -->

<a id="d-req-rag-001"></a>
- **REQ-RAG-001** — Script generation/expansion from a brief (EN/HI) via TechieRag, surfaced in the Avatar-studio script-assist panel. *Also checked:* a one-line brief expands to a usable narration script in EN and HI. (BRD-53)
  - *Acceptance:* When a Creator expands a one-line brief in Avatar studio script assist, then an English or Hindi script appears.

<a id="d-req-rag-002"></a>
- **REQ-RAG-002** — Caption/subtitle text produced from Whisper transcripts via TechieRag (consumed by REQ-FN-040 for burn-in/SRT). *Also checked:* an audio track yields timed caption text matching its speech. (BRD-54)
  - *Acceptance:* When an audio track is transcribed for Avatar studio, then timed caption text matching the speech is returned.

<a id="d-req-rag-003"></a>
- **REQ-RAG-003** — Token-budget tracking for text-LLM spend, feeding the spend rollup (REQ-FN-029/068). *Also checked:* LLM calls accrue token counts/cost per user; totals visible in spend views. (BRD-56)
  - *Acceptance:* When a Creator uses an LLM feature in a studio, then its tokens and cost add to their spend.

<a id="d-req-rag-004"></a>
- **REQ-RAG-004** — RAG-backed character bible (persona voice/wording consistency) stored in PgVector on the app's Postgres instance (TechieRag `UsePgVector`), surfaced in the Character-library bible `Sheet`. *Also checked:* script assist retrieves and applies a character's bible entries; bible edits persist per character. (BRD-55)
  - *Acceptance:* When a Creator uses script assist on Avatar studio for a character, then that character's bible entries are applied.

<a id="d-req-rag-005"></a>
- **REQ-RAG-005** — Prompt-crafting chat via TechieRag **direct chat-completion — deliberately NO RAG/vector setup** (ADR-013): `PromptChatService` resolves the picked registry LLM (endpoint + key), sends the session history + new message (with image attachment passed through to vision-capable models, BRD-90), returns the assistant reply, and accrues token usage into the BRD-56 tracker (`SpendChannel.LlmText`). Degrades cleanly ("LLM not configured / unreachable") like REQ-RAG-001. *Also checked:* a chat round-trip returns a usable crafted prompt from the configured LLM; an image-attached message reaches a vision model and is rejected with a clear message on a non-vision model; token usage appears in `/api/v1/rag/usage` and the spend rollup. (BRD-88, BRD-90, BRD-91) · P10 *Added 2026-07-07.*
  - *Acceptance:* When a Creator sends a message on Prompt studio, then the picked model's reply returns as a copyable prompt.

## Non-functional

<!-- Cross-cutting: verified against the whole build, not one screen. -->

<a id="d-req-nfr-001"></a>
- **REQ-NFR-001** — Performance: UI stays responsive during long jobs; progress streamed; first warm local image in seconds. *Also checked:* no UI freeze during a long render; warm T2I completes within seconds locally. (BRD-69)
  - *Acceptance:* When a long render runs, then the screen stays responsive and progress keeps streaming.

<a id="d-req-nfr-002"></a>
- **REQ-NFR-002** — Scalability: orchestrator scales by adding workers; never assumes a single GPU. *Also checked:* two workers process the queue concurrently without code change. (BRD-70)
  - *Acceptance:* When two workers are attached to the queue, then they process jobs concurrently without a code change.

<a id="d-req-nfr-003"></a>
- **REQ-NFR-003** — Portability: `TrStudioApi`/workers run on macOS (Apple Silicon) + Linux (RunPod); `TrStudio`/`TrStudioAdmin` ship as Blazor Web + MAUI Blazor Hybrid (Win/Mac). *Also checked:* API builds/runs on both OS targets; apps build for Web + MAUI heads. (BRD-71, BRD-87)
  - *Acceptance:* When each target is built and launched, then the API runs on macOS and Linux and both app heads open.

<a id="d-req-nfr-004"></a>
- **REQ-NFR-004** — Reliability: durable jobs (Hangfire/Postgres); worker crash → retry or graceful fail without losing the queue. *Also checked:* killing a worker mid-job leaves the queue intact and the job retried/failed gracefully. (BRD-72)
  - *Acceptance:* When a worker is killed mid-job, then the queue stays intact and the job retries or fails cleanly.

<a id="d-req-nfr-005"></a>
- **REQ-NFR-005** — Security: API auth on every endpoint; encrypted provider-key store; no secrets in logs/URLs; least-privilege workers. *Also checked:* unauthenticated calls rejected; a log/URL sweep finds zero secrets. (BRD-73)
  - *Acceptance:* When logs and URLs are swept after a run, then zero secrets are found.

<a id="d-req-nfr-006"></a>
- **REQ-NFR-006** — Privacy: local jobs stay on the machine; UI clearly flags when a job routes to a closed API (HeyGen/aggregator). *Also checked:* local jobs make no external calls; closed-API routing always shows the banner. (BRD-74)
  - *Acceptance:* When a local job runs, then no external call is made; a closed-API job shows the routing banner.

<a id="d-req-nfr-007"></a>
- **REQ-NFR-007** — Cost control: per-job estimates; RunPod idle auto-stop; monthly cap + alerts across RunPod and closed APIs. *Also checked:* estimates precede every paid run; cap alerts fire; idle workers stop. (BRD-75)
  - *Acceptance:* When a paid job is about to run, then a cost estimate is shown first.

<a id="d-req-nfr-008"></a>
- **REQ-NFR-008** — Observability: Serilog structured logging + OpenTelemetry metrics (queue, memory, latency, spend), traceable job IDs end-to-end. *Also checked:* a job ID traces from submit through provider to asset in logs + metrics. (BRD-76)
  - *Acceptance:* When a job id is traced in logs and metrics, then it links submit, provider and asset.

<a id="d-req-nfr-009"></a>
- **REQ-NFR-009** — Extensibility: registry-driven UI; a new model = a config row + (for ComfyUI) a workflow import — never a dev task. *Also checked:* a new registry row becomes creator-usable with zero code change. (BRD-77)
  - *Acceptance:* When a new registry row is added, then Creators can use it with zero code change.

<a id="d-req-nfr-010"></a>
- **REQ-NFR-010** — Compliance: provenance metadata on outputs; auditable generation log; configurable retention; DPDP-aligned (data resident in India). *Also checked:* retention config honored; audit + provenance verifiable on every output. (BRD-78)
  - *Acceptance:* When the Owner saves "Keep log rows for N days" on Content policy, then log and audit rows older than N days are gone and every picture and clip stays.
  - *Changed 2026-11-19 (owner decision 19A):* retention removes old log rows only (ADR-030).
