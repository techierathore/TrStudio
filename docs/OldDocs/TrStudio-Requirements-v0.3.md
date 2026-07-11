# TrStudio — Product Requirements & Technical Feasibility

**Techie Rathore Studio · An avatar-led, self-hostable AI image & video generation platform**

Version 0.3 (Ready for `*day1-greenfield` hand-off) · June 2026
Prepared as an implementation brief. Markdown so it can be handed directly to an AI coding agent.

---

## 0. How to read this (and what changed since v0.2)

This revision locks the remaining open decisions, renames the application surface for clarity, modernises the technology stack, and removes dependencies that PgVector makes redundant. It is intended to be handed to the `*day1-greenfield TrStudio` command to generate the brief, BRD, architecture, mockups, coding standards and project status.

**Read §3 and §10 first.** §3 is the hardware/budget reality that decides what runs where. §10 is the safety boundary that decides what gets built at all.

**Major changes from v0.2**

| Area | v0.2 | v0.3 (this doc) |
|---|---|---|
| Application naming | "Studio Admin" + "Studio UI" (confusing) | **`TrStudio`** (creator app) + **`TrStudioAdmin`** (model/provider management app) + **`TrStudioApi`** (backend) + **`TrStudioUI`** (shared Razor Class Library) |
| UI delivery | Web now, desktop "later" | **Both `TrStudio` and `TrStudioAdmin` ship as Blazor Web *and* MAUI Blazor Hybrid desktop**, sharing the `TrStudioUI` RCL built on **TrBlazeUI** controls |
| Avatar quality bar | Open question | **HeyGen-grade realism is the target** |
| Closed avatar provider | Aggregator only | **HeyGen API** is the **Premium** avatar tier (user-selectable per job); aggregator handles image/video |
| Persona realism | Open question | **Both photoreal *and* stylized personas supported**; avatar studio defaults to photoreal |
| Hindi scope | Open question | **Hindi voice + captions only** — no Hindi UI |
| Data residency | Open question | TrStudio's **own** PostgreSQL+PgVector, deployed **with the app** in **India** (DPDP-aligned). **AppManager** = external identity/licensing only, *not* the app DB |
| Identity & licensing | Local ASP.NET Identity (implied) | Delegated to **AppManager** child-app API (JWT + RSA-encrypted passwords); feature/tier gating via AppManager FeatureSvc |
| Datastore | PostgreSQL + object storage + **Redis** | **PostgreSQL + PgVector** (vector *and* relational) + object storage; **Redis removed** — durable queue via **Hangfire (Postgres storage)**, cache via in-process `IMemoryCache` |
| Logging | Generic "structured logging" | **Serilog** (structured) + OpenTelemetry |
| .NET version | .NET 8/9 | **.NET 10** (LTS, released Nov 2025) |
| Priority labels | `M` / `S` / `C` (cryptic) | Spelled out as **Must / Should / Could** (MoSCoW) |

---

## 1. Decisions locked (your answers)

| # | Decision | Locked value |
|---|---|---|
| 20 | Personal-first vs SaaS | **Personal-first, SaaS-ready abstractions** via your own application manager (AppManager) |
| 21 | Hardware & budget | **M4 Max, 14-core CPU / 32-core GPU, 36 GB unified memory, ~400 GB free for models**; **RunPod/cloud ≤ $100/mo** |
| 22 | Open vs closed models | **Both** — local open models for personal/testing + **~$50/mo** closed-API access |
| 23 | V1 focus | **Avatar-led** (you want to stay off-camera) |
| 24 | "No Python" constraint | **Not a constraint** — managed/hidden Python workers are fine |
| 25 | App surface & desktop | **`TrStudio`** (creator) and **`TrStudioAdmin`** (management) **both** ship as **Blazor Web + MAUI Blazor Hybrid desktop**, sharing the **`TrStudioUI`** RCL on **TrBlazeUI**. Mac runs the local model stack; Windows is your dev/build box |
| 26 | Content policy | Hard safety floors retained; uncensored/no-filter is **out of scope** (see §10) |
| 27 | Team / build | **Claude Code builds it end-to-end**; you do manual verification, deployment, testing |
| 28 | **Avatar quality bar** | **HeyGen-grade realism** is the target |
| 29 | **Closed avatar provider** | **HeyGen API** is the **Premium** avatar tier, user-selectable per job; an aggregator (fal.ai/Replicate/WaveSpeed) covers closed image/video |
| 30 | **Persona realism** | **Both photoreal and stylized** personas supported; avatar studio defaults to **photoreal** |
| 31 | **Hindi scope** | **Hindi voice + captions only** — UI stays English |
| 32 | **Data residency** | TrStudio keeps its **own** PostgreSQL+PgVector **with the app** (India, DPDP-aligned). **AppManager** handles only identity/licensing/features/payments/support — it is *not* the app database |
| 33 | **Identity & licensing** | Delegated to **AppManager** (child-app integration: API-key + JWT, RSA-encrypted passwords). Drives login, license/subscription, and feature/tier gating |

**Implications of these together (the short version):**

- 36 GB unified memory + a $100 budget means **heavy avatar/video generation cannot live full-time on the Mac, and cannot live on an always-on rented GPU either** — it must run on **serverless, scale-to-zero** GPUs that `TrStudioAdmin` starts and stops on demand. This is a hard design driver, not a nice-to-have (see §3, §13).
- HeyGen-grade realism is reached through a **three-tier quality/cost selector** (Fast = Mac local, High = RunPod self-hosted, **Premium = HeyGen API**) on the same UI control (AV-04).
- Because Claude Code is the builder, the document is written as **explicit, testable requirements with stable IDs** so the agent has unambiguous targets and you have a verification checklist.

---

## 2. Vision and the application surface

**Vision.** One clean app to produce avatar-led videos (a talking presenter that isn't you), plus the supporting images, voices and clips — without ComfyUI graphs or Python setup for the end user — running against models *you* control, on *your* hardware or cheap on-demand GPUs, with a HeyGen-grade Premium tier on tap when you want it.

| Project | Role | Delivery | Runs on |
|---|---|---|---|
| **`TrStudio`** (creator experience) | Avatar studio, image studio, video studio, voice studio, asset gallery, projects. Talks only to `TrStudioApi`; never knows which provider ran a job. | Blazor Web + MAUI Blazor Hybrid desktop | Windows browser / Win+Mac desktop |
| **`TrStudioAdmin`** (model & provider management) | Hardware detection, runtime/worker setup, model + provider registry editing, RunPod start/stop, ComfyUI pipeline import/debug, quotas, cost guardrails, ops view, (later) tenants/billing. A management UI over `TrStudioApi`. | Blazor Web + MAUI Blazor Hybrid desktop | Mac (M4 Max) primarily; server later |
| **`TrStudioApi`** (backend / shared contract) | The unified service surface: provider abstraction, orchestrator, durable job queue, model+provider registry, RunPod control, generation endpoints, auth, cost estimation. **Everything consumed by `TrStudio`, or intended for future third-party/SaaS exposure, lives here.** | ASP.NET Core (.NET 10) | Mac now; deployable to a server later |
| **`TrStudioUI`** (shared Razor Class Library) | Shared Blazor components, layouts and screens built on **TrBlazeUI** controls, consumed by both `TrStudio` and `TrStudioAdmin` across Web and desktop. | Razor Class Library | n/a (referenced) |

> **The `TrStudioAdmin` ↔ `TrStudioApi` boundary.** `TrStudioAdmin` is a *UI*; it performs admin actions by calling `TrStudioApi` (admin-scoped endpoints). Any capability that `TrStudio` also needs, or that you may expose to third parties later, is a **first-class `TrStudioApi` endpoint** — not buried in the admin app. Admin-only operations (setup wizard flows, ComfyUI import/debug, ops dashboards) may have admin-scoped endpoints, but the registry, job, and generation services remain shared in `TrStudioApi`.

> **Identity & licensing live outside TrStudio.** Users, login/JWT, licenses/subscriptions, feature & tier gating, payments and support issues are provided by **AppManager** (see `docs/AppManager-api-usage-guide.md`). `TrStudioApi` integrates as an AppManager *child application* (`X-Api-Key`/`X-Api-Secret` + JWT bearer, RSA-encrypted passwords). TrStudio stores only a reference to the AppManager `userId` for ownership of characters/jobs/assets — **never passwords**. **AppManager is not the application database.**

---

## 3. Hardware & budget reality — what runs where

This is the single most important section for build planning. Your M4 Max with **36 GB unified memory** is excellent for a control plane and for image work, **marginal** for local video, and your **$100/month** only stretches if heavy work is bursty and scale-to-zero.

### 3.1 What the M4 Max (36 GB) can do locally

- **Unified memory** means ~**34 GB usable** for AI (macOS takes 1–2 GB). No separate VRAM wall — a big advantage over a 24 GB consumer GPU for *fitting* models.
- **Memory bandwidth is ~546 GB/s** vs an RTX 4090's ~1008 GB/s, so Apple Silicon is roughly **3–4× slower** for diffusion/video than a 4090. Fine for iteration, painful for production video.
- **On Mac, use the MPS (Metal) backend and GGUF-quantized models.** Default full-precision diffusion nodes often fail or stall on Apple Silicon; the community pattern is GGUF + low resolution first, then scale up.
- **Practical local verdict:**
  - **Images (SDXL, FLUX via GGUF Q6):** comfortable. ✅
  - **Light avatar/lip-sync (Wav2Lip, SadTalker, MuseTalk, LivePortrait):** runs, usable for short clips. ✅/⚠️
  - **Heavy avatar/video (Hallo3, Hunyuan-Avatar, MultiTalk, Wan-Animate/S2V, Wan/Hunyuan/LTX T2V/I2V):** technically fits in 36 GB only when quantized and at low res, and is **slow**. Treat as **RunPod work**, not local. ❌ for routine use.

### 3.2 The three execution targets (the heart of the architecture)

| Target | Use it for | Cost shape | Notes |
|---|---|---|---|
| **Mac local (MPS + ONNX/ComfyUI)** — *Fast tier* | Image gen, light lip-sync, prototyping, the control plane | Free (your hardware) | Python-free path via **ONNX Runtime (CoreML EP)** for images; ComfyUI (MPS) for everything else local |
| **RunPod serverless (scale-to-zero)** — *High tier* | Heavy avatar, video, batch | **Per-second, pay only while running** | A100 ≈ $1.19–1.50/hr; idle = $0. **Must scale to zero** — a 24/7 A100 pod ≈ $860/mo and would vaporise the budget in 3 days |
| **Closed API (~$50/mo)** — *Premium tier* | Top-tier quality when open models fall short | Per-call / per-second | **HeyGen API** for premium avatars; one **aggregator** (fal.ai / Replicate / WaveSpeed) or direct vendors (Veo/Kling/Seedance) for closed image/video |

### 3.3 Worked budget (personal use, illustrative)

Assuming RunPod serverless A100 at ~$1.50/hr effective ($0.00042/sec), including cold start:

| Job | Approx billed GPU time | Approx cost/job | ~Jobs in $50/mo |
|---|---|---|---|
| Image (A100) | 8–15 s | ~$0.005 | ~10,000 |
| Talking-avatar clip (MuseTalk/LatentSync, ~30 s out) | 60–150 s | ~$0.03–0.06 | ~800–1,600 |
| Heavy video (Wan/Hunyuan ~5 s clip) | 2–5 min | ~$0.05–0.13 | ~400–1,000 |

**Conclusion:** $50 RunPod + $50 closed-API (incl. HeyGen Premium avatars) is **comfortably enough for personal/testing volumes** — *only if* `TrStudioAdmin` enforces scale-to-zero and idle auto-stop. Storage: keep big models on a **RunPod network volume** (~$0.07/GB/mo; a 60–100 GB avatar/video set ≈ $4–7/mo) so workers boot fast without re-downloading. **HeyGen** is metered per render against its own plan — track it under the same monthly cap (ADM-10).

---

## 4. Architecture

### 4.1 Layers

| Layer | Contents | Where |
|---|---|---|
| Experience | **`TrStudio`** + **`TrStudioAdmin`**, each as Blazor Web + MAUI Blazor Hybrid desktop, sharing the **`TrStudioUI`** RCL on **TrBlazeUI** | Windows browser / Win+Mac desktop |
| Control / API | **`TrStudioApi`** — ASP.NET Core (.NET 10): provider abstraction, model+provider registry, orchestrator, durable queue (Hangfire/Postgres), RunPod control, ComfyUI pipeline manager, auth, quotas, cost estimation, SignalR progress | Mac |
| Inference | (a) ONNX Runtime in-process (images); (b) ComfyUI workers (local MPS or RunPod); (c) closed-API adapters (**HeyGen**, aggregator) | Mac + RunPod + cloud |
| Data | TrStudio's **own PostgreSQL + PgVector** (registry/jobs/characters/asset-metadata/generation-log **and** vectors/embeddings) + object storage (assets). **No Redis.** | Deployed with the app (India); local during dev |
| Identity / Licensing (external) | **AppManager** API — users, JWT auth, licenses/subscriptions, feature & tier gating, payments, support issues. `TrStudioApi` is a child application (API-key + JWT). | AppManager service (separate) |

### 4.2 Provider abstraction (the rule that keeps you sane)

`TrStudio` requests *"task T with model M and params P."* `TrStudioApi` resolves **M → a provider** and routes the job. Providers implement one interface:

```
IGenerationProvider
  ├─ supports(task, model) : bool
  ├─ submit(job) : jobId           // async
  ├─ status(jobId) : {state, progress, eta}
  ├─ result(jobId) : assetRef
  └─ estimateCost(job) : money
```

Concrete providers for the MVP:
- `OnnxLocalProvider` — ONNX Runtime, CoreML/CPU EP, image + upscale (*Fast tier*).
- `ComfyUIProvider` — drives a ComfyUI instance (local MPS or RunPod) via its HTTP `/prompt` API + WebSocket progress; one provider serves **all** ComfyUI-backed models (avatar, lip-sync, video) because each model is just a **workflow template** (*Fast/High tiers*).
- `HeyGenProvider` — adapter for the **HeyGen API**, the **Premium** avatar tier (*Premium tier*).
- `ClosedApiProvider` — aggregator (fal.ai/Replicate/WaveSpeed) or direct vendor adapter for closed image/video (*Premium tier*).

### 4.3 Model & Provider Registry (the spine)

Every usable model is a registry row owned by `TrStudioApi`. A **declarative params schema** lets the UI render controls for any model with no code change — so "add a model" is a `TrStudioAdmin` action, not a dev task.

```jsonc
{
  "id": "musetalk-v1",
  "displayName": "MuseTalk (real-time lip-sync)",
  "task": "lip-sync",
  "providerType": "comfyui",
  "tier": "fast",              // fast | high | premium
  "workflowRef": "workflows/musetalk_v1.api.json",
  "hardware": { "minVramGb": 8, "recommended": "A100", "runsOnMac": true },
  "params": [
    { "key": "sourceImage", "type": "image", "required": true },
    { "key": "audio", "type": "audio", "required": true },
    { "key": "resolution", "type": "enum", "values": ["256","512"], "default": "256" }
  ],
  "languages": ["en","hi","language-agnostic"],
  "license": "MIT",            // VERIFY per release
  "safetyClass": "avatar-synthetic-default",   // synthetic-default | real-person-gated
  "cost": { "kind": "serverless-seconds", "estimateSecPerJob": 90 }
}
```

---

## 5. The avatar-led model stack (English + Hindi)

Avatar-led video is a **pipeline of three model types** chained together: **(script/text) → voice (TTS) → talking-face (lip-sync/avatar) → optional upscale/format**. Lip-sync models are largely **language-agnostic** (driven by the audio), so **Hindi support is mostly a TTS problem** — pick a Hindi-capable voice model and any avatar model will animate it. **HeyGen** is available as the Premium tier for the whole avatar step when you want its realism.

### 5.1 Recommended open models (verify licences before any commercial use)

| Stage | Model | Strength | Mac 36 GB? | Licence (verify) |
|---|---|---|---|---|
| **Voice (TTS + clone)** | **XTTS-v2** | 17 langs incl. **Hindi**, 6-sec voice clone, emotion transfer | ✅ | Coqui CPML (non-standard — check commercial terms) |
| Voice | **IndicF5 / IndicParler-TTS (AI4Bharat)** | Best **Hindi/Indic** prosody; IndicParler Apache-licensed | ✅ | Apache-2.0 (IndicParler) |
| Voice | **OmniVoice (k2-fsa, 2026)** | 600+ langs zero-shot, 40× realtime, **Apache-2.0** | ✅ | Apache-2.0 |
| Voice | **Qwen3-TTS (2026)** | Multilingual + clone, large training | ⚠️ | Check |
| Speech-to-text | **Whisper** | Transcription, alignment, captions | ✅ | MIT |
| **Lip-sync (on footage)** | **Wav2Lip** | Best raw sync accuracy, zero-shot, light | ✅ | Check (research) |
| Lip-sync | **LatentSync** | SD-based, strong identity preservation | ⚠️ | Open |
| Lip-sync | **MuseTalk** | Real-time, 256px (pair with GFPGAN upscale) | ✅ | Open |
| **Talking head (from 1 image)** | **SadTalker** | Fast single-image talking head | ✅ | Open |
| Talking head | **LivePortrait** | High-fidelity, emotion-aware portrait animation | ✅/⚠️ | Open |
| Talking head | **Hallo2 / Hallo3** | Long, high-res, expressive (diffusion-transformer) | ❌ RunPod | Hallo2 MIT |
| Audio-driven avatar | **Hunyuan-Avatar / MultiTalk / Wan-Animate / Wan-S2V** | Talking/singing from 1 image+audio, up to minutes, 720p | ❌ RunPod | Varies — verify |
| **Premium avatar (closed)** | **HeyGen API** | HeyGen-grade realism, managed, no GPU to run | n/a (cloud) | Commercial (HeyGen ToS) |
| **Image (persona/character)** | **SDXL / FLUX (GGUF) / Qwen-Image** | Character faces, themed sets, consistency (photoreal **and** stylized) | ✅ | Varies |
| Upscale/restore | **GFPGAN / Real-ESRGAN** | Face restore + 4K | ✅ | Open |
| Video T2V/I2V | **Wan 2.x (Apache-2.0) / LTX / Hunyuan / CogVideoX** | B-roll, motion, image-to-motion | ❌ RunPod | Wan/LTX Apache-2.0 |

### 5.2 The avatar pipeline as the product's signature feature

The MVP's headline workflow (HeyGen-style, self-hosted, with HeyGen on tap):

1. **Create a Character** — generate a synthetic face/persona (SDXL/FLUX), **photoreal or stylized**, lock identity (reference set + seed/embedding, optional LoRA). Reusable across all jobs.
2. **Write a script** (EN or HI) → **TTS** produces voice (cloned or library) in the chosen language.
3. **Animate** — feed the character image + audio to a lip-sync/avatar model → talking clip. Pick the tier: **Fast** (Mac), **High** (RunPod), or **Premium** (HeyGen API).
4. **Polish** — upscale, reframe to 9:16 / 1:1 / 16:9, add captions/music, export MP4.

> **Character consistency** (the recurring hard problem across all four reference platforms) is a **first-class entity** here, not a per-prompt afterthought. A character carries a realism flag (photoreal | stylized) that scopes model choice and the safety class.

---

## 6. ComfyUI pipeline management (Import / Debug / Use)

You asked for a screen to import, debug and use ComfyUI pipelines. ComfyUI is also the engine behind most avatar/video models, so this doubles as how new models get added. **This screen lives in `TrStudioAdmin`**; the registered models it produces are served to `TrStudio` through `TrStudioApi`.

**How it works technically:** ComfyUI exposes an HTTP API — `POST /prompt` (submit a workflow graph as JSON in *API format*), `GET /history/{id}`, `/view` (fetch outputs), and a **WebSocket** for live progress and node-level errors. A workflow's editable inputs (prompt, image, audio, seed, resolution) are specific node fields. `TrStudioApi` maps those fields to UI controls via the registry params schema.

### 6.1 Requirements

| ID | Requirement | Priority |
|---|---|---|
| CFY-01 | Import a ComfyUI workflow (drag-drop the *API-format* JSON, or paste). Parse nodes and list parameterizable inputs. | Must |
| CFY-02 | **Map** workflow inputs → registry params (mark required, set types/defaults, hide internal nodes) so `TrStudio` can render controls automatically. | Must |
| CFY-03 | **Run** an imported workflow against a chosen ComfyUI worker (local MPS or RunPod) and stream progress + previews via WebSocket. | Must |
| CFY-04 | **Debug** view: per-node status, error messages, the exact JSON sent, and missing-model/custom-node detection with install hints. | Must |
| CFY-05 | Detect and (where possible) auto-install required **custom nodes** and model files for an imported workflow. | Should |
| CFY-06 | Save an imported+mapped workflow as a registered model usable from `TrStudio` (no graph visible to end users). | Must |
| CFY-07 | Version workflows; pin a known-good version per registered model. | Should |
| CFY-08 | Sandbox/validate a workflow with a smoke test before it goes live. | Should |

---

## 7. TechieRag integration (confirmed from the reference)

TechieRag is consumed as **NuGet packages** (`TechieRag` + `TechieRag.Embedded`) from **GitHub Packages** (`https://nuget.pkg.github.com/techierathore/index.json`; restore needs a GitHub PAT with `read:packages`). It is a **.NET 10 RAG + LLM** library — referenced, not forked. Confirmed capabilities relevant to TrStudio:

- **PgVector vector store** (`UsePgVector(connectionString)`) — backs the character/prompt memory on the **same PostgreSQL** instance TrStudio already uses (no separate vector DB).
- **Embeddings** incl. **ONNX embedded** (`TechieRag.Embedded` / `UseOnnx`) — the text-embedding path needs no external API.
- **LLM completions** across providers (Ollama / LM Studio / OpenAI-compatible / Anthropic / Gemini), with **streaming**, **structured output**, **tool calling**, **token budgets**, **conversation memory**, and **resilience** (retry/fallback/circuit-breaker).

**Scope note (important):** TechieRag is **text/LLM/RAG only** — it does **not** do image or video diffusion. So the split is:

| Use TechieRag for… | Build separately |
|---|---|
| Script generation/expansion from a brief; EN/HI prompt + script handling; caption/subtitle text from Whisper transcripts; a **RAG-backed character bible** (persona voice/wording consistency) in PgVector; token-budget tracking for text-LLM spend | `OnnxLocalProvider` for **image diffusion** (SDXL/FLUX via ONNX Runtime). TechieRag's ONNX is for *embeddings*, not diffusion — the image path is its own implementation |

**Day-1 action for Claude Code:** add the GitHub Packages source + PAT, reference `TechieRag` (+ `TechieRag.Embedded` for offline embeddings), register with `AddTechieRag(...)` using `UsePgVector` against the app DB, and verify the package licence before any commercial use.

---

## 8. Functional requirements

All requirements below are **in the MVP** (per your decision to combine phases). They carry MoSCoW priority (**Must / Should / Could**) for the *build order* in §12, not as a release gate. IDs: `ADM-*` management/control, `AV-*` avatar, `IMG-*` image, `VID-*` video, `VOX-*` voice, `WS-*` workspace, `API-*` platform API, `CFY-*` ComfyUI (§6), `SAFE-*` safety (§10).

> **App ownership convention.** `ADM-*` requirements are surfaced in **`TrStudioAdmin`** but are backed by services in **`TrStudioApi`**. Any service consumed by `TrStudio` or earmarked for third-party exposure is a first-class `TrStudioApi` endpoint (see §2 boundary note).

### 8.1 Management & control (TrStudioAdmin over TrStudioApi)

| ID | Requirement | Priority |
|---|---|---|
| ADM-01 | Detect host capabilities (Apple Silicon GPU cores, unified memory, MPS/CoreML availability, disk) and show a readiness summary. | Must |
| ADM-02 | Guided setup: install ONNX Runtime; provision a **managed, isolated Python env + ComfyUI** for media models without the user touching a terminal. | Must |
| ADM-03 | Manage the isolated Python/ComfyUI environment so it never collides with system Python. | Must |
| ADM-04 | **RunPod serverless integration**: register an endpoint/template, submit jobs, stream status, and **scale to zero** automatically. Start/stop on demand. | Must |
| ADM-05 | Maintain the Model & Provider Registry (§4.3) in `TrStudioApi` as the single source of truth. | Must |
| ADM-06 | One-click install/import of curated open models (resumable downloads), with size/VRAM/licence shown; store big media models on a RunPod **network volume**. | Must |
| ADM-07 | Configure closed-provider adapters (**HeyGen**, aggregator) with **encrypted** API-key storage; enable per model. | Must |
| ADM-08 | Job queue + orchestrator (Hangfire/Postgres): priority, concurrency caps, retries, durable jobs, back-pressure. | Must |
| ADM-09 | Stream progress; surface actionable failures (e.g. "out of memory — drop to 256px or route to RunPod"). | Must |
| ADM-10 | **Cost guardrails**: per-job cost estimate before run; idle auto-stop for RunPod; monthly spend cap with alerts across RunPod **and** closed APIs (default to your $100). | Must |
| ADM-11 | Generation log (who/model/params/cost/duration/outcome) for history, reproduce, audit. | Must |
| ADM-12 | Ops view: worker health, queue depth, spend-to-date, error rates. | Should |
| ADM-13 | Expose the unified `TrStudioApi` surface (OpenAPI, auth, versioned) that `TrStudio` consumes — designed to also serve third parties later. | Must |

### 8.2 Avatar studio (the lead feature — TrStudio)

| ID | Requirement | Priority |
|---|---|---|
| AV-01 | **Character** entity: create a synthetic persona (image gen), **photoreal or stylized**, lock identity (reference set + seed/embedding, optional LoRA), reuse everywhere. | Must |
| AV-02 | Talking avatar from a character image + script/audio with lip-sync (route to MuseTalk/LatentSync/SadTalker/LivePortrait, RunPod heavy model, or **HeyGen** per quality setting). | Must |
| AV-03 | **Script → video**: type a script (EN/HI), pick character + voice, produce a narrated clip end-to-end. | Must |
| AV-04 | Quality/cost selector per job: **Fast (Mac local)** vs **High (RunPod)** vs **Premium (HeyGen API)** — same UI, different provider. | Must |
| AV-05 | Caption/subtitle generation (from the script or Whisper) in EN/HI; burn-in or sidecar SRT. | Should |
| AV-06 | Optional digital-twin from a short **base video** — **consent-gated** (see SAFE-03); off by default. | Could |
| AV-07 | Multi-shot assembly: chain several avatar clips into one sequence with consistent character. | Could |

> **Tier entitlement.** In personal mode all tiers (Fast/High/**Premium-HeyGen**) are available. When run multi-user/SaaS, which tiers a user may select can be gated via **AppManager** FeatureSvc/LicenseSvc (e.g. Premium behind a paid license).

### 8.3 Voice studio (English + Hindi — TrStudio)

| ID | Requirement | Priority |
|---|---|---|
| VOX-01 | Text-to-speech in **English and Hindi** (and Hinglish), with a library of preset voices. | Must |
| VOX-02 | Voice cloning from a short sample — **consent-gated** for real people (SAFE-03). Synthetic/preset voices need no gate. | Must |
| VOX-03 | Per-language model routing (e.g. IndicF5/XTTS-Hindi for HI; XTTS/OmniVoice for EN) hidden behind one "voice" control. | Must |
| VOX-04 | Emotion/style and speed controls where the model supports them. | Should |
| VOX-05 | Align generated audio to the avatar pipeline automatically (timing + lip-sync handoff). | Must |

### 8.4 Image studio (TrStudio)

| ID | Requirement | Priority |
|---|---|---|
| IMG-01 | Text-to-image with prompt composer + dynamically rendered model params (registry schema). | Must |
| IMG-02 | Image-to-image, inpaint/outpaint, mask editing on a canvas. | Must |
| IMG-03 | Reference-image / ControlNet conditioning (pose/depth/edges); face/identity lock for characters. | Must |
| IMG-04 | Upscale + face restoration to 2K/4K. | Must |
| IMG-05 | Batch/grid variations with seed control + reproducibility. | Should |
| IMG-06 | Social-format export presets (1:1, 4:5, 9:16, 16:9) + background removal. | Should |

### 8.5 Video studio (TrStudio)

| ID | Requirement | Priority |
|---|---|---|
| VID-01 | Image-to-video and text-to-video (open model on RunPod, or closed API) with duration/motion/aspect controls. | Must |
| VID-02 | First/last-frame + reference-image conditioning for consistency. | Should |
| VID-03 | Frame interpolation (RIFE/FILM) and video upscale as post-steps. | Should |
| VID-04 | Trim/join/reframe, add audio (from Voice studio), export MP4 in platform formats. | Must |
| VID-05 | Clip extension/stitching for longer sequences. | Could |

### 8.6 Workspace (TrStudio)

| ID | Requirement | Priority |
|---|---|---|
| WS-01 | Asset gallery: every output browsable/filterable and reusable as an input. | Must |
| WS-02 | Projects/folders; full-parameter history with one-click reproduce. | Must |
| WS-03 | Side-by-side comparison across models/seeds/providers. | Should |
| WS-04 | Prompt + script library; reusable style/voice presets. | Should |
| WS-05 | Export/download (single + bulk) with provenance metadata (SAFE-04). | Must |
| WS-06 | Account + usage/spend view; license/subscription & billing surfaced from **AppManager** (LicenseSvc/PaymentSvc). | Could |

### 8.7 Platform API (SaaS-ready abstraction — TrStudioApi)

| ID | Requirement | Priority |
|---|---|---|
| API-01 | `TrStudioApi` is designed to also serve as an external REST API (API-key auth) later. | Should |
| API-02 | Async job model: submit → poll/stream → fetch, identical across providers. | Must |
| API-03 | Webhooks on completion/failure. | Could |
| API-04 | MCP server exposing generation as agent tools (matches competitor MCP offerings). | Could |

---

## 9. Non-functional requirements

| ID | Category | Requirement |
|---|---|---|
| NFR-01 | Performance | UI responsive during long jobs; stream progress; first local image (warm) in seconds |
| NFR-02 | Scalability | Orchestrator scales by adding workers; never assumes one GPU |
| NFR-03 | Portability | `TrStudioApi`/workers run on macOS (Apple Silicon) + Linux (RunPod containers); `TrStudio`/`TrStudioAdmin` run as Blazor Web (any modern browser) and MAUI Blazor Hybrid desktop (Win/Mac) |
| NFR-04 | Reliability | Durable jobs (Hangfire/Postgres); worker crash → retry or graceful fail without losing the queue |
| NFR-05 | Security | API auth; encrypted provider-key store; no secrets in logs/URLs; least-privilege workers |
| NFR-06 | Privacy | Local jobs stay on your machine; UI clearly flags when a job is routed to a closed API (HeyGen/aggregator) |
| NFR-07 | Cost control | Per-job estimates; RunPod idle auto-stop; monthly cap + alerts across RunPod and closed APIs |
| NFR-08 | Observability | **Serilog** structured logging + OpenTelemetry metrics (queue, memory, latency, spend), traceable job IDs |
| NFR-09 | Extensibility | Registry-driven UI; new model = config + (for ComfyUI) a workflow import |
| NFR-10 | Compliance | Provenance metadata on outputs; auditable generation log; configurable retention; **DPDP-aligned** (data in India) |

---

## 10. Trust, Safety & Legal — the boundary

This section is **binding on the build**. You asked for NSFW/uncensored support; here is exactly what is and isn't in this document, and why.

### 10.1 What is NOT specified here (and won't be)

- **No "uncensored / disable-all-filters" mode.** An open face-swap + avatar + video pipeline with no safety layer is, in practice, a tool for **non-consensual intimate imagery (NCII) and CSAM**. Those harms don't depend on intent, so the no-filter design is out.
- **No sexual content of real, identifiable people** without that person's verified consent; **no minors** in any sexual context, ever.
- **No adult-content feature spec.** If you later pursue *legal adult content from fully-synthetic adult personas*, that's a decision for you + your own legal counsel + your own safety tooling. Nothing in the architecture below blocks you from adding it yourself — but this document doesn't author it.

**Why this also serves your goals:** an uncensored/deepfake stance blocks **Stripe/Razorpay/PayPal**, the **App Store/Play Store**, and most **hosting**, which closes the SaaS path you want to keep open. In India, the **IT Act intermediary rules** and **DPDP Act 2023** (faces/voices are sensitive personal data) make an uncensored deepfake tool a serious liability. And **Claude Code will refuse** to implement filter-removal or non-consensual-deepfake features at build time.

### 10.2 Non-negotiable safety floors (these ARE requirements)

| ID | Requirement | Priority |
|---|---|---|
| SAFE-01 | Prevent CSAM by design: hash-matching + classifier checks on inputs/outputs, hard block + log, on every provider. Not user-disableable. | Must |
| SAFE-02 | Block sexualisation/face-swap of real, identifiable people; refuse NCII/sexual deepfakes. | Must |
| SAFE-03 | Any real-person face or voice clone requires a recorded **consent** step, retained as evidence. | Must |
| SAFE-04 | Embed provenance/AI-disclosure metadata (C2PA-style; respect provider watermarks) on outputs; non-trivial to strip. | Must |
| SAFE-05 | Block impersonation of real public figures by default; allow only with explicit rights. | Must |
| SAFE-06 | Per-deployment policy config, abuse reporting, and a full generation audit trail. | Should |

### 10.3 Legal checklist (for you + counsel before any public/SaaS launch)

- **Per-model licences** in the registry (Wan/LTX Apache-2.0 are cleanest; Hunyuan has a MAU cap; XTTS uses a non-standard licence; **HeyGen** has its own commercial ToS — verify each before commercial use).
- **Likeness/voice/deepfake law** and **DPDP Act** obligations for handling user faces/voices (data resident in India).
- **EU AI Act** transparency (disclose AI-generated media) if serving EU users — SAFE-04 helps.

---

## 11. Final technology stack

| Concern | Choice |
|---|---|
| Creator UI | **`TrStudio`** — Blazor (.NET 10, unified render modes) Web **+** MAUI Blazor Hybrid desktop (Win/Mac) |
| Management UI | **`TrStudioAdmin`** — same stack (Blazor Web + MAUI Blazor Hybrid) |
| Shared UI library | **`TrStudioUI`** — Razor Class Library of shared components/screens built on **TrBlazeUI** controls, referenced by both apps |
| Backend / API | **`TrStudioApi`** — ASP.NET Core (.NET 10); background jobs via **Hangfire (Postgres storage)**; SignalR for progress |
| Local image inference | **ONNX Runtime directly** (CoreML/CPU EP) — *aligns with TechieRag; TensorStack not used* |
| Media inference (avatar/video/voice) | **ComfyUI** (managed) on **Mac MPS** (Fast) and **RunPod serverless** (High) |
| Premium avatar | **HeyGen API** (Premium tier) |
| Text/RAG (script, captions, prompt memory, character bible) | **TechieRag** NuGet (`TechieRag` + `TechieRag.Embedded`) — PgVector store, ONNX embeddings, LLM + tool-calling. **Text only**; not the image path |
| Closed image/video | One **aggregator** adapter first (fal.ai/Replicate/WaveSpeed), vendor adapters (Veo/Kling/Seedance) later |
| Data | TrStudio's **own PostgreSQL + PgVector** (registry, jobs, characters, asset metadata, generation log **and** vectors/embeddings) + object storage, deployed **with the app in India** (DPDP-aligned). **No Redis** — durable queue via Hangfire/Postgres, cache via in-process `IMemoryCache`. *AppManager is a separate service and is **not** the application database.* |
| Identity & licensing | **AppManager** (external child-app API) — registration/login/JWT/refresh with RSA-encrypted passwords (AuthSvc), license validation (LicenseSvc), feature/tier gating (FeatureSvc), payments & support (PaymentSvc/IssueSvc). `TrStudioApi` authenticates with `X-Api-Key`/`X-Api-Secret` + JWT bearer. **No local password store** |
| Packaging | Docker for `TrStudioApi` + workers; RunPod template image for the GPU worker |
| Logging / Observability | **Serilog** (structured) + OpenTelemetry metrics dashboard |

> **Why ONNX Runtime over TensorStack (your call, and I agree):** you already use ONNX Runtime directly in TechieRag, it removes a thin third-party dependency with a tiny maintainer base, and it gives you full control of the image path. ComfyUI covers the media models TensorStack couldn't do well anyway.
>
> **Why no Redis:** PgVector already gives you vector + relational in one PostgreSQL instance; Hangfire's Postgres storage gives durable background jobs; `IMemoryCache` covers in-process caching. Adding Redis would mean a second datastore to run, secure and back up for no MVP-level benefit. Revisit only if you later need cross-node distributed cache or pub/sub at scale.

---

## 12. MVP build order (single MVP, sequenced for the agent)

You want everything in the MVP. To keep Claude Code's work verifiable, build it in this dependency order — each step is independently testable by you.

1. **Skeleton & spine.** Solution scaffolding (`TrStudio`, `TrStudioAdmin`, `TrStudioApi`, `TrStudioUI`); provider abstraction + Model/Provider Registry; Hangfire/Postgres job queue; auth; storage; OpenAPI; Serilog. *Verify: a stub job flows `TrStudio` → `TrStudioApi` → fake worker → gallery.*
2. **Local image vertical.** `OnnxLocalProvider` + SDXL/FLUX (GGUF) on Mac; Image studio (IMG-01..04); gallery/history (WS-01/02). *Verify: generate + reproduce an image locally.*
3. **Managed ComfyUI + RunPod.** ADM-02/03/04; `ComfyUIProvider`; RunPod serverless scale-to-zero + cost guardrails (ADM-10). *Verify: one ComfyUI job runs locally and on RunPod, idle-stops, cost shown.*
4. **Voice (EN+HI).** VOX-01..05 with XTTS/IndicF5/OmniVoice. *Verify: EN and HI speech + a cloned synthetic voice.*
5. **Avatar pipeline (lead feature).** Character entity (AV-01, photoreal+stylized), lip-sync/avatar (AV-02), script→video (AV-03), quality/cost selector incl. **HeyGen Premium** (AV-04), captions (AV-05). *Verify: a full EN and HI talking-avatar clip from a synthetic character across all three tiers.*
6. **ComfyUI pipeline screen (TrStudioAdmin).** CFY-01..08 so new models are importable. *Verify: import a new lip-sync workflow and use it from `TrStudio`.*
7. **Video + closed API.** VID-01..04; aggregator adapter + `HeyGenProvider` (ADM-07). *Verify: I2V locally/RunPod and one closed-API generation.*
8. **Safety + provenance throughout.** SAFE-01/04 wired into every provider from step 2; SAFE-02/03/05 enforced before any real-person feature. *Verify: blocks fire; provenance present on exports.*
9. **Polish + SaaS-ready seams.** Comparison view, prompt/script library, spend dashboard, `TrStudioApi` API-key seam (API-01/02).

---

## 13. Risk register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| 36 GB too small for heavy local video/avatar | High | High | Route heavy jobs to RunPod or HeyGen; Mac for image + light lip-sync only |
| Budget blown by an always-on GPU | High | High | **Serverless scale-to-zero only**; idle auto-stop; monthly cap across RunPod + closed APIs (ADM-10) |
| Closed-model/aggregator/HeyGen price changes | Med | Med | Keep open-model parity; closed API + HeyGen optional behind the tier selector |
| Model licence breach in SaaS | Med | High | Licence field + build-time check; prefer Apache-2.0; legal review pre-launch |
| Safety/abuse (CSAM, NCII, deepfakes) | Med | **Critical** | SAFE-01..06 as hard, non-disableable floors; no uncensored mode |
| ComfyUI on Apple Silicon flakiness (MPS/GGUF) | Med | Med | GGUF + low-res-first defaults; auto-fallback to RunPod on OOM |
| "Everything in MVP" → unverifiable blob | Med | Med | The §12 build order keeps each step independently testable |
| TechieRag / TrBlazeUI reuse assumptions wrong | Med | Low | Confirm against the real repos at day-1 before depending on them (§7, §11) |

---

## 14. Resolved decisions & residual open items

**Resolved in v0.3** (were §14 open questions in v0.2):

1. **Avatar quality bar** → **HeyGen-grade realism**.
2. **Closed-API choice** → **HeyGen API** as the Premium avatar tier; aggregator (fal.ai/Replicate/WaveSpeed) for image/video.
3. **TechieRag** → confirmed: consumed as NuGet (`TechieRag` / `TechieRag.Embedded`), PgVector + ONNX embeddings, **text/LLM only** (§7).
4. **Hindi scope** → **Voice + captions only**; UI stays English.
5. **Persona realism** → **Both** photoreal and stylized; avatar studio defaults to photoreal.
6. **Data residency** → TrStudio's **own** PostgreSQL+PgVector **with the app** in **India**; AppManager is external identity/licensing only.
7. **HeyGen** → **pay-as-you-go**, tracked under the **$50** closed-API cap (ADM-10).
8. **TrBlazeUI** → confirmed: .NET 10 Blazor + Tailwind v4 + shadcn/ui lib (`.trblazeui/`); `TrStudioUI` builds on it and `*mockups` reads its catalog.
9. **AppManager** → external identity/licensing/feature/payment/support service (`docs/AppManager-api-usage-guide.md`); **not** the app DB.

**Residual items — credentials/keys needed at build/run, not blocking the docs:**

- **GitHub PAT** (`read:packages`) to restore the TechieRag NuGet packages.
- **HeyGen API key** (pay-as-you-go).
- **AppManager API key/secret** for the `TrStudioApi` child-application registration.

---

## Appendix A — Self-hostable model quick reference (mid-2026, verify licences)

| Model | Stage | Mac 36 GB | Licence (verify) |
|---|---|---|---|
| SDXL / FLUX (GGUF) / Qwen-Image | Image | ✅ | Varies |
| XTTS-v2 | Voice (EN+HI, clone) | ✅ | Coqui CPML |
| IndicF5 / IndicParler-TTS | Voice (Hindi/Indic) | ✅ | Apache (IndicParler) |
| OmniVoice | Voice (600+ langs) | ✅ | Apache-2.0 |
| Whisper | Speech-to-text | ✅ | MIT |
| Wav2Lip / MuseTalk / SadTalker / LivePortrait | Lip-sync / talking head | ✅ | Varies |
| LatentSync | Lip-sync | ⚠️ | Open |
| Hallo2/3, Hunyuan-Avatar, MultiTalk, Wan-Animate/S2V | Heavy avatar | ❌ RunPod | Varies |
| Wan 2.x / LTX / Hunyuan / CogVideoX | Video T2V/I2V | ❌ RunPod | Wan/LTX Apache-2.0 |
| GFPGAN / Real-ESRGAN / RIFE | Upscale / interpolate | ✅ | Open |
| **HeyGen API** | **Premium avatar (closed)** | n/a (cloud) | Commercial (HeyGen ToS) |

## Appendix B — Glossary

- **MPS** — Metal Performance Shaders, Apple's GPU compute backend (the Mac equivalent of CUDA).
- **CoreML EP** — ONNX Runtime's Apple-hardware execution provider.
- **GGUF** — a quantized model format; the practical way to fit big models on Apple Silicon.
- **Serverless (scale-to-zero)** — GPU billed per second only while running; idle = free.
- **Lip-sync vs talking-head** — lip-sync edits mouth on existing footage; talking-head animates a still image (pose, eyes, expression) from audio.
- **MoSCoW** — Must / Should / Could / Won't prioritisation; this doc uses Must/Should/Could.
- **RCL** — Razor Class Library; shared Blazor components consumed by multiple apps.
- **PgVector** — PostgreSQL extension for vector similarity search; lets one DB hold relational + vector data.
- **C2PA** — content-provenance standard for disclosing AI-generated media.
- **NCII** — non-consensual intimate imagery.
- **DPDP Act** — India's Digital Personal Data Protection Act, 2023.

*Findings reflect mid-2026 and date quickly; verify model availability, performance and licences at build time.*
