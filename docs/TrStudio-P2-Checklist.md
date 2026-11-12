# TrStudio — Checklist (phase 2)

| | |
|---|---|
| App | TrStudio |
| Size | Medium |
| Phase | 2 of 2 |

## Goal

Phase 2 adds Venice as a second picture and video provider beside ComfyUI, so a creator can generate without a local ComfyUI. An admin enters Venice, its address, key and models on Registry & providers; everything is stored in the database, the key encrypted. The studios offer a Venice model only for the tasks it can do; each call's price is stored and shown in the run details; Venice's own reason is shown on failure. ComfyUI is unchanged. Source: [phase 2 BRD](TrStudio-P2-BRD.md).

## Requirements Status

| ID | Requirement | Status | % | Remarks | Details |
|----|-------------|--------|---|---------|---------|
| REQ-FN-075 | Add Venice as a provider with its address and key | Implemented | 75% | [REQ-FN-075] Provider save/list carry BaseUrl beside the key; listing masks the key. VeniceRegistryTests passes; migration 1500_VeniceProvider. No screen smoke this run (owner); Registry & providers screen is the UI wave. | [view](#d-req-fn-075) |
| REQ-FN-076 | The Venice key is stored encrypted, like the other provider keys | Implemented | 75% | [REQ-FN-076] Key sealed by AesGcmKeyStore; only ciphertext reaches ProviderKey; no read returns it. VeniceRegistryTests passes. No screen smoke this run (owner). | [view](#d-req-fn-076) |
| REQ-UI-083 | Test the Venice connection | In Progress | 50% | [REQ-UI-083] built in src/TrStudioUI/Pages/Admin/AdminRegistry.razor; screen smoke not run (owner: no browser check this run) | [view](#d-req-ui-083) |
| REQ-UI-084 | Add a Venice model as a registry row for one task | In Progress | 50% | [REQ-UI-084] built in src/TrStudioUI/Pages/Admin/AdminRegistry.razor; screen smoke not run (owner: no browser check this run) | [view](#d-req-ui-084) |
| REQ-FN-077 | Record whether a Venice model takes a reference picture or keeps a fixed character | Implemented | 75% | [REQ-FN-077] TakesReferencePicture, KeepsFixedCharacter, ProviderModelId, MaxInputImages persist on both upsert branches and read back. VeniceRegistryTests passes. No screen smoke this run (owner). | [view](#d-req-fn-077) |
| REQ-FN-078 | Venice needs nothing in appsettings or code | Implemented | 75% | [REQ-FN-078] Address and key read from the database per call. VeniceProviderTests.TextToImageUsesDatabaseAddressAndKey and SettingsSourceRuleTests.VeniceLivesOnlyInTheDatabase pass. No screen smoke this run (owner). | [view](#d-req-fn-078) |
| REQ-UI-085 | Venice picture models appear in the model list | In Progress | 50% | [REQ-UI-085] built in Pages/Image/ImageStudio.razor + ImageModelFilter.cs; ImageModelFilterTests (OptionLabel, NoReferenceNoMask) pass, unit 364/364; screen smoke not run (owner: no browser check this run) | [view](#d-req-ui-085) |
| REQ-UI-086 | Generate a picture through Venice | In Progress | 50% | [REQ-UI-086] built in Pages/Image/ImageStudio.razor; tile shows the returned picture via new owner-only GET /api/v1/assets/{id}/content (AssetContentTypesTests); unit 369/369; screen smoke not run (owner: no browser check this run) | [view](#d-req-ui-086) |
| REQ-UI-087 | A reference picture limits the list to models that take one | In Progress | 50% | [REQ-UI-087] built in Pages/Image/ImageStudio.razor + ImageModelFilter.cs; ImageModelFilterTests.ReferencePictureKeepsOnlyRowsThatTakeOne passes, unit 364/364; screen smoke not run (owner: no browser check this run) | [view](#d-req-ui-087) |
| REQ-FN-079 | Edit a picture through a Venice edit model | Implemented | 75% | [REQ-FN-079] Source sent as data URL to /image/edit (multi-edit for several); edited picture stored as the job's asset. VeniceProviderTests passes. No screen smoke this run (owner). | [view](#d-req-fn-079) |
| REQ-UI-088 | A masked edit is never offered to Venice | In Progress | 50% | [REQ-UI-088] built in Pages/Image/ImageStudio.razor + ImageModelFilter.cs; ImageModelFilterTests.MaskHidesEveryVeniceRow passes, unit 364/364; screen smoke not run (owner: no browser check this run) | [view](#d-req-ui-088) |
| REQ-FN-080 | Upscale through Venice | Implemented | 75% | [REQ-FN-080] Scale 2 sent to /image/upscale; stored picture is twice as wide. VeniceProviderTests.UpscaleTwoTimesStoresPictureTwiceAsWide passes. No screen smoke this run (owner). | [view](#d-req-fn-080) |
| REQ-UI-089 | Venice text-to-video models appear in a model list | In Progress | 50% | [REQ-UI-089] built in Pages/Video/VideoStudio.razor + VideoModelFilter.cs; VideoModelFilterTests pass (unit 364/364); screen smoke not run (owner: no browser check this run) | [view](#d-req-ui-089) |
| REQ-UI-090 | Make a clip through Venice with progress | In Progress | 50% | [REQ-UI-090] built in Pages/Video/VideoStudio.razor (venice-progress, Venice price, alert-venice-failed); VideoModelFilterTests pass; screen smoke not run (owner: no browser check this run) | [view](#d-req-ui-090) |
| REQ-UI-091 | Image to video offers only models that can do it | In Progress | 50% | [REQ-UI-091] built in Pages/Video/VideoStudio.razor + VideoModelFilter.cs; VideoModelFilterTests.ImageToVideo_lists_only_image_to_video_rows passes; screen smoke not run (owner: no browser check this run) | [view](#d-req-ui-091) |
| REQ-UI-092 | A chosen character offers only models that keep a fixed character | In Progress | 50% | [REQ-UI-092] built in Pages/Video/VideoStudio.razor + VideoModelFilter.cs; VideoModelFilterTests.A_chosen_character_lists_only_fixed_character_rows passes; screen smoke not run (owner: no browser check this run) | [view](#d-req-ui-092) |
| REQ-FN-081 | Duration, resolution and aspect follow the chosen model | Implemented | 75% | [REQ-FN-081] Video studio controls fill from the picked row via VideoModelFilter; VideoModelFilterTests.Allowed_values_follow_the_picked_Venice_model and Switching_model_moves_out_of_range_choices… pass; back end VeniceProviderTests pass; no screen smoke this run (owner). | [view](#d-req-fn-081) |
| REQ-FN-082 | Store Venice's price for each call with the generation | Implemented | 75% | [REQ-FN-082] Log row stores ActualCost and PriceSource (venice-list / venice-quote); asset and run DTOs carry them. VenicePriceRecordTests passes. No screen smoke this run (owner). | [view](#d-req-fn-082) |
| REQ-UI-093 | Show the provider and price in the run details on Projects | In Progress | 50% | [REQ-UI-093] built in src/TrStudioUI/Pages/Projects/Projects.razor; screen smoke not run (owner: no browser check this run) | [view](#d-req-ui-093) |
| REQ-UI-094 | Show the provider and price in the asset details on Gallery | In Progress | 50% | [REQ-UI-094] built in src/TrStudioUI/Pages/Gallery/Gallery.razor; screen smoke not run (owner: no browser check this run) | [view](#d-req-ui-094) |
| REQ-UI-095 | A failed or refused call shows Venice's own reason | In Progress | 50% | [REQ-UI-095] built in src/TrStudioUI/Pages/Projects/Projects.razor; screen smoke not run (owner: no browser check this run) | [view](#d-req-ui-095) |
| REQ-FN-085 | Venice spend counts toward the monthly cap | Implemented | 75% | [REQ-FN-085] Venice price checked against the cap before sending; over cap returns the existing 402 SPEND_CAP_EXCEEDED, nothing queued or generated. VeniceOrchestrationTests passes. No screen smoke this run (owner). | [view](#d-req-fn-085) |
| REQ-FN-083 | The safety floors run before any Venice call | Implemented | 75% | [REQ-FN-083] Tripped floor refuses with zero HTTP calls to Venice (fake handler). VeniceOrchestrationTests.TrippedSafetyFloorMakesNoVeniceCall passes. No screen smoke this run (owner). | [view](#d-req-fn-083) |
| REQ-FN-084 | Venice's safe mode follows the owner's content policy | Implemented | 75% | [REQ-FN-084] safe_mode true unless unrestricted mode is effective. VeniceProviderTests.SafeModeFollowsOwnerPolicy passes. No screen smoke this run (owner). | [view](#d-req-fn-084) |
| REQ-UI-096 | The studio says when a prompt leaves the machine | In Progress | 50% | [REQ-UI-096] built in Pages/Image/ImageStudio.razor + ImageModelFilter.cs; ImageModelFilterTests pass, unit 364/364; screen smoke not run (owner: no browser check this run) | [view](#d-req-ui-096) |
| REQ-NFR-011 | ComfyUI behaves exactly as before Venice | Implemented | 75% | [REQ-NFR-011] All 16 ComfyUI tests pass; ComfyUiProvider.cs and the five Comfy test files have the same sha256 before and after. VeniceOrchestrationTests.ComfyUiRowStillResolvesToComfyUiProvider passes. No screen smoke this run (owner). | [view](#d-req-nfr-011) |
| REQ-NFR-012 | One live test per task type against Venice | In Progress | 50% | [REQ-NFR-012] Five Category=Live tests in VeniceLiveTests, out of the normal run. Run on request 2026-11-12: 5 skipped, "testsettings.Local.json is not valid JSON (line 5)" (missing closing brace; no Venice:BaseUrl). Owner fixes the file, then reruns. | [view](#d-req-nfr-012) |
| REQ-NFR-013 | Live tests skip clearly without a key | In Progress | 50% | [REQ-NFR-013] VeniceLiveFact skips with "Venice key not set in testsettings.Local.json" on an empty key; this run showed the invalid-file skip line instead, since the local file holds a key but is not valid JSON. | [view](#d-req-nfr-013) |
| REQ-NFR-014 | Test settings come from a file beside the tests, never environment variables | Implemented | 75% | [REQ-NFR-014] TestSettings reads testsettings(.Local).json; LiveApiFixture's DOTNET_ROOT/HOME reads moved to the file. SettingsSourceRuleTests.TestsReadNoEnvironmentVariables passes. No screen smoke this run (owner). | [view](#d-req-nfr-014) |
| REQ-UI-097 | Fill a Venice model's details from Venice's own model list | In Progress | 50% | [REQ-UI-097] built in src/TrStudioUI/Pages/Admin/AdminRegistry.razor; screen smoke not run (owner: no browser check this run) | [view](#d-req-ui-097) |
| REQ-NFR-015 | The desktop app reads its settings from a file, never environment variables | Implemented | 75% | [REQ-NFR-015] MauiProgram reads Desktop:ApiBaseUrl, DevRole, DevStartPath from embedded appsettings.json over the template; MAUI head builds. SettingsSourceRuleTests.DesktopReadsSettingsFromFile passes. No screen smoke this run (owner). | [view](#d-req-nfr-015) |

**Status values:** `Not Started` · `In Progress` · `Implemented` · `Verified` · `Done (pre-existing)` · `Needs re-verify` · `PARTIAL` · `FAIL` · `Blocked` · `Owner-UAT` · `N/A`.

## Page: Admin — Model & Provider Registry

<a id="d-req-fn-075"></a>
- **REQ-FN-075** — Add Venice as a provider with its address and key. *BRD:* BRD-93 · *Mockup:* mockups/admin-registry.html
  - *Acceptance:* When an admin saves Venice's address and key on Registry & providers, then Venice is listed, key masked.

<a id="d-req-fn-076"></a>
- **REQ-FN-076** — The Venice key is stored encrypted, like the other provider keys. *BRD:* BRD-94 · *Mockup:* mockups/admin-registry.html
  - *Acceptance:* When an admin saves a Venice key on Registry & providers, then only ciphertext is stored and never returned.

<a id="d-req-ui-083"></a>
- **REQ-UI-083** — Test the Venice connection. *BRD:* BRD-95 · *Mockup:* mockups/admin-registry.html
  - *Acceptance:* When an admin presses Test connection on Registry & providers, then it shows Connected or Venice's own error text.

<a id="d-req-ui-084"></a>
- **REQ-UI-084** — Add a Venice model as a registry row for one task. *BRD:* BRD-96 · *Mockup:* mockups/admin-registry.html
  - *Acceptance:* When an admin saves a Venice model row on Registry & providers, then it lists with a Venice badge.

<a id="d-req-fn-077"></a>
- **REQ-FN-077** — Record whether a Venice model takes a reference picture or keeps a fixed character. *BRD:* BRD-97 · *Mockup:* mockups/admin-registry.html
  - *Acceptance:* When an admin ticks reference picture and fixed character on Registry & providers, then both ticks remain after reload.

<a id="d-req-fn-078"></a>
- **REQ-FN-078** — Venice needs nothing in appsettings or code. *BRD:* BRD-98 · *Mockup:* mockups/admin-registry.html
  - *Acceptance:* When a creator generates on Image studio with no Venice appsettings entry, then Venice uses the database address.

<a id="d-req-ui-097"></a>
- **REQ-UI-097** — Fill a Venice model's details from Venice's own model list. *BRD:* BRD-122 · *Mockup:* mockups/admin-registry.html
  - *Acceptance:* When an admin presses Fill from Venice on Registry & providers, then task, abilities, durations and sizes fill in, unsaved.

## Page: Image studio

<a id="d-req-ui-085"></a>
- **REQ-UI-085** — Venice picture models appear in the model list. *BRD:* BRD-99 · *Mockup:* mockups/image-studio.html
  - *Acceptance:* When a creator opens Image studio, then the model list shows enabled Venice text-to-image rows badged Cloud · Venice.

<a id="d-req-ui-086"></a>
- **REQ-UI-086** — Generate a picture through Venice. *BRD:* BRD-100 · *Mockup:* mockups/image-studio.html
  - *Acceptance:* When a creator generates with a Venice model on Image studio, then a result tile shows Venice's picture.

<a id="d-req-ui-087"></a>
- **REQ-UI-087** — A reference picture limits the list to models that take one. *BRD:* BRD-101 · *Mockup:* mockups/image-studio.html
  - *Acceptance:* When a creator adds a reference picture on Image studio, then the model list shows only reference-picture rows.

<a id="d-req-fn-079"></a>
- **REQ-FN-079** — Edit a picture through a Venice edit model. *BRD:* BRD-102 · *Mockup:* mockups/image-studio.html
  - *Acceptance:* When a creator edits a source picture with a Venice image-to-image row on Image studio, then the edited picture appears.

<a id="d-req-ui-088"></a>
- **REQ-UI-088** — A masked edit is never offered to Venice. *BRD:* BRD-103 · *Mockup:* mockups/image-studio.html
  - *Acceptance:* When a creator draws a mask on Image studio, then the model list shows no Venice row.

<a id="d-req-fn-080"></a>
- **REQ-FN-080** — Upscale through Venice. *BRD:* BRD-104 · *Mockup:* mockups/image-studio.html
  - *Acceptance:* When a creator upscales 2x with the Venice upscale row on Image studio, then a picture twice as wide appears.

<a id="d-req-fn-085"></a>
- **REQ-FN-085** — Venice spend counts toward the monthly cap. *BRD:* BRD-114 · *Mockup:* mockups/image-studio.html
  - *Acceptance:* When a Venice price would pass the monthly cap on Image studio, then nothing is sent; the over-cap alert shows.

<a id="d-req-fn-083"></a>
- **REQ-FN-083** — The safety floors run before any Venice call. *BRD:* BRD-115 · *Mockup:* mockups/image-studio.html
  - *Acceptance:* When a prompt trips a safety floor on Image studio, then nothing is sent to Venice.

<a id="d-req-fn-084"></a>
- **REQ-FN-084** — Venice's safe mode follows the owner's content policy. *BRD:* BRD-116 · *Mockup:* mockups/image-studio.html
  - *Acceptance:* When unrestricted mode is off and a creator generates on Image studio, then the Venice request carries safe mode on.

<a id="d-req-ui-096"></a>
- **REQ-UI-096** — The studio says when a prompt leaves the machine. *BRD:* BRD-117 · *Mockup:* mockups/image-studio.html
  - *Acceptance:* When a creator picks a Venice model on Image studio, then a notice says prompt and pictures go to Venice.

## Page: Video studio

<a id="d-req-ui-089"></a>
- **REQ-UI-089** — Venice text-to-video models appear in a model list. *BRD:* BRD-105 · *Mockup:* mockups/video-studio.html
  - *Acceptance:* When a creator opens Text to video on Video studio, then the model list shows text-to-video rows, Venice ones badged.

<a id="d-req-ui-090"></a>
- **REQ-UI-090** — Make a clip through Venice with progress. *BRD:* BRD-106 · *Mockup:* mockups/video-studio.html
  - *Acceptance:* When a creator generates with a Venice model on Video studio, then progress shows until the clip plays.

<a id="d-req-ui-091"></a>
- **REQ-UI-091** — Image to video offers only models that can do it. *BRD:* BRD-107 · *Mockup:* mockups/video-studio.html
  - *Acceptance:* When a creator switches to Image to video on Video studio, then the model list shows only image-to-video rows.

<a id="d-req-ui-092"></a>
- **REQ-UI-092** — A chosen character offers only models that keep a fixed character. *BRD:* BRD-108 · *Mockup:* mockups/video-studio.html
  - *Acceptance:* When a creator picks a character on Video studio, then the model list shows only fixed-character rows.

<a id="d-req-fn-081"></a>
- **REQ-FN-081** — Duration, resolution and aspect follow the chosen model. *BRD:* BRD-109 · *Mockup:* mockups/video-studio.html
  - *Acceptance:* When a creator picks a Venice video model on Video studio, then duration, resolution and aspect offer only its values.

## Page: Projects

<a id="d-req-fn-082"></a>
- **REQ-FN-082** — Store Venice's stated price ("Venice price": video quote, picture list price) with the generation. *BRD:* BRD-110 · *Mockup:* mockups/projects.html
  - *Acceptance:* When a Venice generation finishes on Image studio, then its generation-log row holds Venice's price and price source.

<a id="d-req-ui-093"></a>
- **REQ-UI-093** — Show the provider and price in the run details on Projects. *BRD:* BRD-111 · *Mockup:* mockups/projects.html
  - *Acceptance:* When a creator selects a Venice run on Projects, then Run parameters shows provider Venice and its US-dollar price.

<a id="d-req-ui-095"></a>
- **REQ-UI-095** — A failed or refused call shows Venice's own reason. *BRD:* BRD-113 · *Mockup:* mockups/projects.html
  - *Acceptance:* When Venice refuses a call from Image studio, then the failure alert and Projects run details show Venice's own text.

## Page: Gallery

<a id="d-req-ui-094"></a>
- **REQ-UI-094** — Show the provider and price in the asset details on Gallery. *BRD:* BRD-112 · *Mockup:* mockups/gallery.html
  - *Acceptance:* When a creator opens a Venice asset on Gallery, then the detail panel shows provider Venice and its price.

## Non-functional

<a id="d-req-nfr-011"></a>
- **REQ-NFR-011** — ComfyUI behaves exactly as before Venice. *BRD:* BRD-118
  - *Acceptance:* When the tests run after Venice is added, then every ComfyUI test passes and no ComfyUI test file changed.

<a id="d-req-nfr-012"></a>
- **REQ-NFR-012** — One live test per task type against Venice, run only on request at the cheapest settings. *BRD:* BRD-119
  - *Acceptance:* When the live tests are run on request with a Venice key, then all five task-type tests pass.

<a id="d-req-nfr-013"></a>
- **REQ-NFR-013** — Live tests skip clearly without a key. *BRD:* BRD-120
  - *Acceptance:* When no Venice key is in testsettings.Local.json, then each live test skips saying "Venice key not set in testsettings.Local.json".

<a id="d-req-nfr-014"></a>
- **REQ-NFR-014** — Test settings come from a file beside the tests, never environment variables. *BRD:* BRD-121
  - *Acceptance:* When any test needs a setting, then it reads the testsettings files; tests/ contains no environment-variable read.

<a id="d-req-nfr-015"></a>
- **REQ-NFR-015** — The desktop app reads its settings from a file, never environment variables. *BRD:* BRD-123
  - *Acceptance:* When the desktop app starts, then its API address, role and start page come from appsettings.json, not environment variables.

