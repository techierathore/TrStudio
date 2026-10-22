# TrStudio → TechieRag library feedback

One entry per gap/observation found while integrating TechieRag. This file goes to the TechieRag team
(per CLAUDE.md "Verification"). Do not silently work around library issues — record them here.

## Summary
- 0 blockers, 1 major, 3 minor. Major (RAG-005): **no image / multimodal input seam** on `ILlmProvider` at 1.0.5 (`TR-RAG-001`). Minor (all under RAG-004): (1) `IngestTextAsync` metadata is `Dictionary<string,object>?` vs the reference's `<string,string>?`; (2) no metadata/tenant filter on `SearchAsync` (must over-fetch + re-filter in-process); (3) embedding-provider outage surfaces as an unwrapped `HttpRequestException` (no typed "embeddings unavailable" signal).
- Impact: all worked around; RAG-004 + RAG-005 acceptance met. RAG-005 text chat uses `ILlmProvider.ChatAsync` (confirmed present at 1.0.5); vision falls back to a direct OpenAI-compatible multimodal POST. Package pinned **TechieRag 1.0.5** (`net10.0`).
- Last consolidated: 2026-07-07

## RAG-005 — Prompt Studio chat (direct chat-completion, no RAG), Phase 10

Context: `PromptChatService` (ADR-013) drives the picked `text-chat` registry LLM through TechieRag's
**direct chat-completion** path (no RAG/vector retrieval). Because 1.0.5's shared client exposes no
per-call endpoint override, each call builds a fresh `TechieRagBuilder` client from the row's own
endpoint/key/model (a `UseSqliteVec` temp store satisfies `Build()` but is never ingested/searched) and
sends history + the new message via `ILlmProvider.ChatAsync(IReadOnlyList<ChatMessage>, options, ct)`.
Package pinned: **TechieRag 1.0.5** (`net10.0`).

### TR-RAG-001. No image / multimodal input seam on `ILlmProvider`

> ✅ **Closed 2026-10-18** — fixed in TechieRag 1.1.7. `ChatMessage.UserWithImages(text, images)` and the `ChatContentPart` / `ChatImage` model are present; every built-in provider that supports vision (OpenAI-compatible, LM Studio, Anthropic, Gemini, Ollama) encodes the parts in its own wire format.

- **Severity:** Major (blocks the BRD-90 vision path through the library; text chat is unaffected).
- **Repro:** Configure a vision-capable OpenAI-compatible model. There is no way to attach image bytes:
  `ChatMessage.Content` is a single `string` (no content-parts / image-url list), `LlmCompletionOptions`
  has no image field, and `ILlmProvider` has no image/multimodal overload. Verified by reflection over
  `TechieRag.dll` 1.0.5 (`ChatMessage`, `LlmCompletionOptions`, `ILlmProvider` members).
- **Expected:** a way to pass an image (bytes + media type, or a data/HTTP URL) alongside the text of a
  user turn — e.g. `ChatMessage.User(text, images)` or content-parts — so vision models can be driven
  through `ChatAsync`.
- **Actual:** image input is impossible via the TechieRag API; only text turns can be sent.
- **Workaround:** for a vision-capable **OpenAI-compatible** endpoint, `PromptChatService` bypasses
  TechieRag for the image case and POSTs a standard multimodal chat-completion directly over
  `HttpClient` (a `content` array with an `image_url` data-URI). Non-OpenAI-compatible families
  (Anthropic/Gemini) degrade cleanly with a clear message. Non-vision rows are rejected up front (BRD-90).
- **Suggested fix:** add multimodal content support to `ChatMessage` / `ILlmProvider.ChatAsync` (content
  parts with text + image url/bytes), mirroring the OpenAI/Anthropic multimodal message shape.

Note: `ILlmProvider.ChatAsync` (multi-turn text) **is** present at 1.0.5 — the AI reference doc's "v2"
signatures for text chat match the shipped assembly; only the multimodal/image seam is missing.

## RAG-004 — Character bible (PgVector), Phase 5

Context: per-character persona notes are the durable source of truth in Postgres
(`"CharacterBibleEntry"`, owner + character scoped) and are additionally indexed into PgVector via
TechieRag for semantic script-assist retrieval (`CharacterBibleService`). Package pinned:
**TechieRag 1.0.5** (`net10.0`).

### 1. `IngestTextAsync` metadata parameter type
- **Observed:** `IngestTextAsync(text, documentId, metadata, ct)` takes
  `Dictionary<string, object>?` in 1.0.5, whereas the bundled AI reference
  (`.techierag/TechieRag-AI-Reference.md`, generated for v2.0) documents
  `Dictionary<string, string>?`.
- **Impact:** compile error until the dictionary value type was changed to `object`; retrieved
  `SearchResult.Chunk.Metadata` values also come back as `object`, so callers must `?.ToString()`
  before parsing (e.g. `Guid.TryParse`).
- **Ask:** align the reference doc with the shipped signature, or standardise on
  `Dictionary<string,string>` for metadata (string metadata is the common case for scoping filters).

### 2. No metadata/tenant filter on `SearchAsync`
- **Observed:** `SearchAsync(query, topK, documentFilter, ct)` only accepts a `documentFilter`
  (document-id scoped); there is no metadata predicate (e.g. filter by `characterId` / `userId`).
- **Impact:** for owner + character scoped retrieval we must over-fetch and re-apply the
  owner/character filter in-process against the persisted rows (defence-in-depth for BRD-81). Fine at
  small scale but does not push the tenant filter down into the vector store.
- **Ask:** a metadata equality filter on `SearchAsync` (and/or a per-collection/namespace scope) so
  multi-tenant vector retrieval can be scoped server-side.

### 3. Embedding-provider outage surfaces as an unwrapped transport exception
- **Observed:** with the default Ollama embedding endpoint unreachable (`localhost:11434` refused),
  `InitializeAsync` / `SearchAsync` / `IngestTextAsync` throw the raw
  `HttpRequestException`/`SocketException` from `OllamaEmbeddingProvider.EmbedAsync`.
- **Impact:** we wrap every TechieRag call in try/catch and degrade to a deterministic keyword match
  over the persisted entries (this is the RAG-004 "degrades without an embedding model" path, working
  as intended on our side). But every caller must know to catch broadly.
- **Ask:** a typed `TechieRagEmbeddingUnavailableException` (or a `TryX`/health probe) so callers can
  branch on "embeddings unavailable" without catching `Exception`.

### Status
- RAG-004 acceptance met regardless of the above: bible entries persist per character (owner-scoped),
  the retrieval path is wired through TechieRag/PgVector, and it degrades cleanly to keyword matching
  when no embedding model is running. `UsePgVector` is confirmed present in 1.0.5 and is wired onto
  the app Postgres connection (`RagModule`).
