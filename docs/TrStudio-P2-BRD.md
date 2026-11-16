# TrStudio — Business Requirements — Phase 2: Venice cloud provider

| | |
|---|---|
| App | TrStudio |
| Kind | app |
| Size | Medium |
| Phase | 2 of 3 |
| Status | Draft |
| Date | 2026-11-12 |

## 1. Summary

Today TrStudio makes pictures and videos only through ComfyUI, or the local ONNX model, on the creator's own machine or on RunPod. Phase 2 adds Venice (venice.ai) as a second provider behind the same provider seam: `ProviderKind`, `RegistryModel` and `GenerationTaskType`. A creator can then make pictures and videos without a local ComfyUI. An admin enters the Venice provider, its address, its key and its models on the existing Registry & providers screen. All of it is stored in the database, with the key encrypted like the other provider keys. Nothing about Venice goes in appsettings or in code. Each registry row names a Venice model and the one task it does. The studios offer a Venice model only for the tasks it can do. The price of each Venice call is stored with the generation and shown in the run details. When Venice refuses or fails a call, the creator sees Venice's own reason. ComfyUI keeps working exactly as before.

## 2. Screens and flow

This phase adds no new screen. It adds controls to five phase-1 screens, which are listed in [phase 1](TrStudio-BRD.md). In each mockup the new controls are marked `data-tf-phase="2"`.

| Screen | Route | Role | Mockup | Fields |
|---|---|---|---|---|
| Admin — Model & Provider Registry (phase 1) | `/admin/registry` | Operator | [mockup](mockups/admin-registry.html) | provider name, address, API key, enabled; model id, task, reference picture, fixed character |
| Image studio (phase 1) | `/image` | Creator | [mockup](mockups/image-studio.html) | model, prompt, reference picture, source picture |
| Video studio (phase 1) | `/video` | Creator | [mockup](mockups/video-studio.html) | model, character, duration, resolution, aspect |
| Projects (phase 1) | `/projects` | Creator | [mockup](mockups/projects.html) | provider, Venice price, failure reason |
| Gallery (phase 1) | `/gallery` | Creator | [mockup](mockups/gallery.html) | provider, Venice price |

**Primary journey:**
1. The admin opens Registry & providers and adds the provider "Venice" with the address `https://api.venice.ai/api/v1` and a Venice API key. They press Test connection and see "Connected".
2. The admin adds registry rows. Each row holds one Venice model id and the task it does, for example `z-image-turbo` for text to image or `wan-3-0-image-to-video` for image to video. The admin ticks the row's abilities and enables it.
3. A creator opens Image studio or Video studio, picks the Venice model (it carries a "Cloud · Venice" badge) and generates.
4. The result appears like any other result. Its run details on Projects and Gallery show provider Venice and Venice's price for the call.

## 3. Requirements

An acceptance line is at most 30 words and holds one behaviour. Each line is what a unit, integration or browser test asserts.

### Admin — Model & Provider Registry

The admin sets up Venice here: the provider, its address and key, and one registry row per Venice model and task.

- **BRD-93** — Add Venice as a provider with its address and key. *Screen:* Admin — Model & Provider Registry · *Mockup:* [mockup](mockups/admin-registry.html)
  - *Acceptance:* When an admin saves Venice's address and key on Registry & providers, then Venice is listed, key masked.
- **BRD-94** — The Venice key is stored encrypted, like the other provider keys. *Screen:* Admin — Model & Provider Registry · *Mockup:* [mockup](mockups/admin-registry.html)
  - *Acceptance:* When an admin saves a Venice key on Registry & providers, then only ciphertext is stored and never returned.
- **BRD-95** — Test the Venice connection. *Screen:* Admin — Model & Provider Registry · *Mockup:* [mockup](mockups/admin-registry.html)
  - *Acceptance:* When an admin presses Test connection on Registry & providers, then it shows Connected or Venice's own error text.
- **BRD-96** — Add a Venice model as a registry row for one task. *Screen:* Admin — Model & Provider Registry · *Mockup:* [mockup](mockups/admin-registry.html)
  - *Acceptance:* When an admin saves a Venice model row on Registry & providers, then it lists with a Venice badge.
- **BRD-97** — Record whether a Venice model takes a reference picture or keeps a fixed character. *Screen:* Admin — Model & Provider Registry · *Mockup:* [mockup](mockups/admin-registry.html)
  - *Acceptance:* When an admin ticks reference picture and fixed character on Registry & providers, then both ticks remain after reload.
- **BRD-98** — Venice needs nothing in appsettings or code. *Screen:* Admin — Model & Provider Registry · *Mockup:* [mockup](mockups/admin-registry.html)
  - *Acceptance:* When a creator generates on Image studio with no Venice appsettings entry, then Venice uses the database address.

- **BRD-122** — Fill a Venice model's details from Venice's own model list. *Screen:* Admin — Model & Provider Registry · *Mockup:* [mockup](mockups/admin-registry.html)
  - *Acceptance:* When an admin presses Fill from Venice on Registry & providers, then task, abilities, durations and sizes fill in, unsaved.

### Image studio

Venice picture models appear in the same model list as local models and are offered only for the tasks they can do.

- **BRD-99** — Venice picture models appear in the model list. *Screen:* Image studio · *Mockup:* [mockup](mockups/image-studio.html)
  - *Acceptance:* When a creator opens Image studio, then the model list shows enabled Venice text-to-image rows badged Cloud · Venice.
- **BRD-100** — Generate a picture through Venice. *Screen:* Image studio · *Mockup:* [mockup](mockups/image-studio.html)
  - *Acceptance:* When a creator generates with a Venice model on Image studio, then a result tile shows Venice's picture.
- **BRD-101** — A reference picture limits the list to models that take one. *Screen:* Image studio · *Mockup:* [mockup](mockups/image-studio.html)
  - *Acceptance:* When a creator adds a reference picture on Image studio, then the model list shows only reference-picture rows.
- **BRD-102** — Edit a picture through a Venice edit model. *Screen:* Image studio · *Mockup:* [mockup](mockups/image-studio.html)
  - *Acceptance:* When a creator edits a source picture with a Venice image-to-image row on Image studio, then the edited picture appears.
- **BRD-103** — A masked edit is never offered to Venice. *Screen:* Image studio · *Mockup:* [mockup](mockups/image-studio.html)
  - *Acceptance:* When a creator draws a mask on Image studio, then the model list shows no Venice row.
- **BRD-104** — Upscale through Venice. *Screen:* Image studio · *Mockup:* [mockup](mockups/image-studio.html)
  - *Acceptance:* When a creator upscales 2x with the Venice upscale row on Image studio, then a picture twice as wide appears.

### Video studio

Video studio gains a model list under the tier choice, filled from the registry, so a creator can pick a Venice video model.

- **BRD-105** — Venice text-to-video models appear in a model list. *Screen:* Video studio · *Mockup:* [mockup](mockups/video-studio.html)
  - *Acceptance:* When a creator opens Text to video on Video studio, then the model list shows text-to-video rows, Venice ones badged.
- **BRD-106** — Make a clip through Venice with progress. *Screen:* Video studio · *Mockup:* [mockup](mockups/video-studio.html)
  - *Acceptance:* When a creator generates with a Venice model on Video studio, then progress shows until the clip plays.
- **BRD-107** — Image to video offers only models that can do it. *Screen:* Video studio · *Mockup:* [mockup](mockups/video-studio.html)
  - *Acceptance:* When a creator switches to Image to video on Video studio, then the model list shows only image-to-video rows.
- **BRD-108** — A chosen character offers only models that keep a fixed character. *Screen:* Video studio · *Mockup:* [mockup](mockups/video-studio.html)
  - *Acceptance:* When a creator picks a character on Video studio, then the model list shows only fixed-character rows.
- **BRD-109** — Duration, resolution and aspect follow the chosen model. *Screen:* Video studio · *Mockup:* [mockup](mockups/video-studio.html)
  - *Acceptance:* When a creator picks a Venice video model on Video studio, then duration, resolution and aspect offer only its values.

### Run details: price, failure, safety

The price and the outcome of each Venice call are kept with the generation, and the safety rules apply before anything leaves the machine.

- **BRD-110** — Store Venice's stated price for each call with the generation, shown as "Venice price": the quote for a video, the listed price for a picture. *Screen:* Projects · *Mockup:* [mockup](mockups/projects.html)
  - *Acceptance:* When a Venice generation finishes on Image studio, then its generation-log row holds Venice's price and price source.
- **BRD-111** — Show the provider and price in the run details on Projects. *Screen:* Projects · *Mockup:* [mockup](mockups/projects.html)
  - *Acceptance:* When a creator selects a Venice run on Projects, then Run parameters shows provider Venice and its US-dollar price.
- **BRD-112** — Show the provider and price in the asset details on Gallery. *Screen:* Gallery · *Mockup:* [mockup](mockups/gallery.html)
  - *Acceptance:* When a creator opens a Venice asset on Gallery, then the detail panel shows provider Venice and its price.
- **BRD-113** — A failed or refused call shows Venice's own reason. *Screen:* Projects · *Mockup:* [mockup](mockups/projects.html)
  - *Acceptance:* When Venice refuses a call from Image studio, then the failure alert and Projects run details show Venice's own text.
- **BRD-114** — Venice spend counts toward the monthly cap. *Screen:* Image studio · *Mockup:* [mockup](mockups/image-studio.html)
  - *Acceptance:* When a Venice price would pass the monthly cap on Image studio, then nothing is sent; the over-cap alert shows.
- **BRD-115** — The safety floors run before any Venice call. *Screen:* Image studio · *Mockup:* [mockup](mockups/image-studio.html)
  - *Acceptance:* When a prompt trips a safety floor on Image studio, then nothing is sent to Venice.
- **BRD-116** — Venice's safe mode follows the owner's content policy. *Screen:* Image studio · *Mockup:* [mockup](mockups/image-studio.html)
  - *Acceptance:* When unrestricted mode is off and a creator generates on Image studio, then the Venice request carries safe mode on.
- **BRD-117** — The studio says when a prompt leaves the machine. *Screen:* Image studio · *Mockup:* [mockup](mockups/image-studio.html)
  - *Acceptance:* When a creator picks a Venice model on Image studio, then a notice says prompt and pictures go to Venice.

## 4. Non-functional requirements

| Id | Area | Requirement | Measure |
|---|---|---|---|
| BRD-118 | Compatibility | ComfyUI behaves exactly as before Venice | When the unit and integration tests run after Venice is added, then every ComfyUI test passes and no file under `tests/unit/TrStudio.Tests.Unit/Comfy/` changed. |
| BRD-119 | Testing | One live test per task type against Venice, run only on request at the cheapest settings | When the live tests are run on request with a Venice key in the test settings file, then the text-to-image, image-to-image, upscale, text-to-video and image-to-video tests pass; the normal test run leaves them out. |
| BRD-120 | Testing | Live tests skip clearly without a key | When the live tests run with no Venice key in the test settings file, then each is skipped with the line "Venice key not set in testsettings.Local.json". |
| BRD-121 | Testing | Test settings come from a file beside the tests, never environment variables | When any test needs a setting, then it reads it from the testsettings files, and a search of `tests/` finds no environment-variable read. |
| BRD-123 | Settings | The desktop app reads its settings from a file, never environment variables | When the desktop app starts, then it takes its API address, developer role and start page from appsettings.json and reads no environment variable. |

## 5. Development status

Written by the status gate after every build, verify and handoff; not by hand.

**Snapshot as of 2026-11-16.** Live per-requirement status: `PROJECT-STATUS.md` and the Requirements Status table in `docs/TrStudio-P2-Checklist.md`.

| Screen | Requirements | Verified | Open | Status |
|---|---|---|---|---|
| Admin — Model & Provider Registry | 7 | 7 | 0 | Done |
| Image studio | 10 | 10 | 0 | Done |
| Video studio | 5 | 5 | 0 | Done |
| Projects | 3 | 3 | 0 | Done |
| Gallery | 1 | 1 | 0 | Done |
| Non-functional | 5 | 5 | 0 | Done |

## 6. Where the rest lives

| What | Where |
|---|---|
| Scope, users and roles, the context diagram | [phase 1 BRD](TrStudio-BRD.md) |
| Non-functional requirements for the whole application | [phase 1 BRD](TrStudio-BRD.md) |
| Constraints, assumptions and risks | [phase 1 BRD](TrStudio-BRD.md) |
| Every phase, its screens and its BRD range | [TrStudio-Phases.md](TrStudio-Phases.md) |
| This phase's work list | [TrStudio-P2-Checklist.md](TrStudio-P2-Checklist.md) |
| The Venice provider design and its decision | [Architecture §5 and ADR-014](TrStudio-Architecture.md) |
