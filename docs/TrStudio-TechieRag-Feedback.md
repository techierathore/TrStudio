# TechieRag feedback — found while building TrStudio

| | |
|---|---|
| App | TrStudio |
| Upstream | TechieRag |
| Updated | 2026-11-12 |

## Summary

4 entries: 0 blocking now, 3 filed and not blocking, 1 fixed upstream.

Nothing is blocked. The prompt-chat picture input (TR-RAG-001) was fixed in TechieRag 1.1.7 and closed on 2026-10-18. The three minor character-bible gaps (TR-RAG-002 to TR-RAG-004) are all worked around in TrStudio; the character bible and prompt chat both meet their acceptance lines. TrStudio pins TechieRag 1.0.5 (`net10.0`).

## Entries

### TR-RAG-001 — No way to send a picture to a language model through `ILlmProvider`

Fixed upstream: closed 2026-10-18, fixed in TechieRag 1.1.7 (`ChatMessage.UserWithImages(text, images)` with `ChatContentPart` / `ChatImage`; every built-in vision provider encodes the parts in its own wire format).

- **Severity:** major
- **Blocks:** no — text chat worked; the picture case posted directly to the model, and the work carried on
- **Repro:** configure a vision-capable OpenAI-compatible model and look for a way to attach image bytes: `ChatMessage.Content` is one `string`, `LlmCompletionOptions` has no image field, `ILlmProvider` has no image overload (checked by reflection over `TechieRag.dll` 1.0.5)
- **Expected:** a way to pass an image (bytes and media type, or a data or web address) with the text of a user turn, so vision models work through `ChatAsync`
- **Actual:** only text turns can be sent
- **Encountered in:** REQ-RAG-005 (Prompt studio, BRD-90)
- **Workaround:** for an OpenAI-compatible vision endpoint, `PromptChatService` posts a multimodal chat-completion directly over `HttpClient` (a `content` array with an `image_url` data address); Anthropic and Gemini rows degrade with a clear message; non-vision rows are refused up front
- **Suggested fix:** content parts (text plus image address or bytes) on `ChatMessage` and `ILlmProvider.ChatAsync`, mirroring the OpenAI and Anthropic multimodal message shape

### TR-RAG-002 — `IngestTextAsync` metadata type differs from the reference document

- **Severity:** minor
- **Blocks:** no — the dictionary value type was changed to `object` and the work carried on
- **Repro:** call `IngestTextAsync(text, documentId, metadata, ct)` with a `Dictionary<string, string>` as `.techierag/TechieRag-AI-Reference.md` (written for v2.0) documents
- **Expected:** the shipped signature matches the reference: `Dictionary<string, string>?`
- **Actual:** 1.0.5 takes `Dictionary<string, object>?`; compile error, and `SearchResult.Chunk.Metadata` values come back as `object`, so callers must call `?.ToString()` before parsing (for example `Guid.TryParse`)
- **Encountered in:** REQ-RAG-004 character bible, `CharacterBibleService`
- **Workaround:** values typed as `object` and converted to text on read
- **Suggested fix:** align the reference document with the shipped signature, or standardise on `Dictionary<string, string>` for metadata

### TR-RAG-003 — `SearchAsync` cannot filter by metadata such as owner or character

- **Severity:** minor
- **Blocks:** no — results are over-fetched and filtered in memory, and the work carried on
- **Repro:** call `SearchAsync(query, topK, documentFilter, ct)` and try to limit results to one `characterId` or `userId`
- **Expected:** a metadata equality filter, or a per-collection scope, applied inside the vector store
- **Actual:** only `documentFilter` (document id) exists
- **Encountered in:** REQ-RAG-004 character bible (owner and character scoping, BRD-81)
- **Workaround:** fetch more results and re-apply the owner and character filter against the stored rows
- **Suggested fix:** a metadata filter on `SearchAsync`, so a multi-user vector search is scoped on the server

### TR-RAG-004 — An embedding-service outage surfaces as a raw network exception

- **Severity:** minor
- **Blocks:** no — every TechieRag call is wrapped and falls back to keyword matching, and the work carried on
- **Repro:** stop the default Ollama embedding endpoint (`localhost:11434` refused) and call `InitializeAsync`, `SearchAsync` or `IngestTextAsync`
- **Expected:** a typed signal that embeddings are unavailable
- **Actual:** the raw `HttpRequestException` / `SocketException` from `OllamaEmbeddingProvider.EmbedAsync` reaches the caller
- **Encountered in:** REQ-RAG-004 character bible ("degrades without an embedding model" path)
- **Workaround:** broad try/catch around every TechieRag call and a keyword match over the stored entries
- **Suggested fix:** a typed `TechieRagEmbeddingUnavailableException`, or a health check or `TryX` method

#### Detail

The character bible keeps per-character notes in Postgres (`"CharacterBibleEntry"`, owner and character scoped) and also indexes them into PgVector through TechieRag (`UsePgVector` on the app's Postgres connection, `RagModule`) for script-assist search. Prompt Studio (ADR-013) builds a fresh `TechieRagBuilder` client per call from the picked registry row's endpoint, key and model, because 1.0.5's shared client has no per-call endpoint override; a temporary `UseSqliteVec` store satisfies `Build()` and is never used. `ILlmProvider.ChatAsync` for multi-turn text is present at 1.0.5 and matches the reference.
