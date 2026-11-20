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
| REQ-FN-075 | Add Venice as a provider with its address and key | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-FN-075 Venice address and key saved on Registry, key mas` | [view](#d-req-fn-075) |
| REQ-FN-076 | The Venice key is stored encrypted, like the other provider keys | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-FN-076 VeniceKeyIsEncryptedAndNeverReturned` | [view](#d-req-fn-076) |
| REQ-UI-083 | Test the Venice connection | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-UI-083 Test connection shows Venice own error text, then` | [view](#d-req-ui-083) |
| REQ-UI-084 | Add a Venice model as a registry row for one task | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-UI-084 a saved Venice model row lists with a Venice badg` | [view](#d-req-ui-084) |
| REQ-FN-077 | Record whether a Venice model takes a reference picture or keeps a fixed character | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-FN-077 RegistrySqlRoundTripsAbilityTicks(aColumn: "Provi` | [view](#d-req-fn-077) |
| REQ-FN-078 | Venice needs nothing in appsettings or code | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-FN-078 VeniceLivesOnlyInTheDatabase` | [view](#d-req-fn-078) |
| REQ-UI-085 | Venice picture models appear in the model list | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-UI-085 OptionLabelBadgesVeniceRows` | [view](#d-req-ui-085) |
| REQ-UI-086 | Generate a picture through Venice | Needs re-verify | 75% | ⚠ UAT bug 2026-11-20: With the real Venice API (venice-sd35, 20 Nov 2026 walkthrough check on the test host) Generate ends on Ready with no result tile and no message: the studio polls the job only 8 x 250 ms (ImageStudio.razor:746-760), then reads an empty result while Venice is still drawing. The picture is made and saved (Gallery lists it).… | [view](#d-req-ui-086) |
| REQ-UI-087 | A reference picture limits the list to models that take one | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-UI-087 ReferencePictureKeepsOnlyRowsThatTakeOne` | [view](#d-req-ui-087) |
| REQ-FN-079 | Edit a picture through a Venice edit model | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-FN-079 ImageToImageSendsSourceAsDataUrl` | [view](#d-req-fn-079) |
| REQ-UI-088 | A masked edit is never offered to Venice | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-UI-088 MaskHidesEveryVeniceRow` | [view](#d-req-ui-088) |
| REQ-FN-080 | Upscale through Venice | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-FN-080 UpscaleTwoTimesStoresPictureTwiceAsWide` | [view](#d-req-fn-080) |
| REQ-UI-089 | Venice text-to-video models appear in a model list | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-UI-089 TextToVideoListsVeniceRowsBadged` | [view](#d-req-ui-089) |
| REQ-UI-090 | Make a clip through Venice with progress | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-UI-090 VideoShowsProgressUntilTheClipIsReady` | [view](#d-req-ui-090) |
| REQ-UI-091 | Image to video offers only models that can do it | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-UI-091 ImageToVideoListsOnlyImageToVideoRows` | [view](#d-req-ui-091) |
| REQ-UI-092 | A chosen character offers only models that keep a fixed character | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-UI-092 ChosenCharacterListsOnlyFixedCharacterRows(aImage` | [view](#d-req-ui-092) |
| REQ-FN-081 | Duration, resolution and aspect follow the chosen model | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-FN-081 AllowedValuesFollowThePickedVeniceModel` | [view](#d-req-fn-081) |
| REQ-FN-082 | Store Venice's price for each call with the generation | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-FN-082 SucceededVeniceRunLogsPriceAndSource` | [view](#d-req-fn-082) |
| REQ-UI-093 | Show the provider and price in the run details on Projects | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-UI-093 RunDetailsShowVeniceUsDollarPrice(aCost: "0", aEx` | [view](#d-req-ui-093) |
| REQ-UI-094 | Show the provider and price in the asset details on Gallery | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-UI-094 DetailsShowVeniceLinesForVeniceAssets(aProvider: ` | [view](#d-req-ui-094) |
| REQ-UI-095 | A failed or refused call shows Venice's own reason | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-UI-095 FailureAlertKeepsVeniceTextVerbatim` | [view](#d-req-ui-095) |
| REQ-FN-085 | Venice spend counts toward the monthly cap | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-FN-085 OverCapShowsTheOverCapAlert` | [view](#d-req-fn-085) |
| REQ-FN-083 | The safety floors run before any Venice call | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-FN-083 TrippedSafetyFloorMakesNoVeniceCall(aTask: TextTo` | [view](#d-req-fn-083) |
| REQ-FN-084 | Venice's safe mode follows the owner's content policy | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-FN-084 SafeModeFollowsOwnerPolicy(aUnrestricted: True, a` | [view](#d-req-fn-084) |
| REQ-UI-096 | The studio says when a prompt leaves the machine | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-UI-096 picking a Venice model shows the sent-to-Venice n` | [view](#d-req-ui-096) |
| REQ-NFR-011 | ComfyUI behaves exactly as before Venice | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-NFR-011 ComfyUiRowStillResolvesToComfyUiProvider` | [view](#d-req-nfr-011) |
| REQ-NFR-012 | One live test per task type against Venice | Verified | 100% | 2026-11-18 verify: not observable — NFR row with no perf-budget and no unit test carrying its id; Verified on 2026-11-15 stands, not re-measured | [view](#d-req-nfr-012) |
| REQ-NFR-013 | Live tests skip clearly without a key | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-NFR-013 LiveTestsSkipWithTheKeyLineWhenNoKeyIsSet` | [view](#d-req-nfr-013) |
| REQ-NFR-014 | Test settings come from a file beside the tests, never environment variables | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-NFR-014 TestsReadNoEnvironmentVariables` | [view](#d-req-nfr-014) |
| REQ-UI-097 | Fill a Venice model's details from Venice's own model list | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-UI-097 FillReferenceToVideo` | [view](#d-req-ui-097) |
| REQ-NFR-015 | The desktop app reads its settings from a file, never environment variables | Verified | 100% | 2026-11-18 verify: PASS — test `REQ-NFR-015 DesktopReadsSettingsFromFile` | [view](#d-req-nfr-015) |

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

