# TechieRag feedback — found while building TrStudio

| | |
|---|---|
| App | TrStudio |
| Upstream | TechieRag |
| Updated | 2026-11-15 |

## Summary

6 entries: 0 blocking now, 5 filed and not blocking, 1 fixed upstream (TR-RAG-001).

Nothing is blocked. On 15 Nov 2026 TrStudio moved from 1.0.5 to TechieRag 1.1.5, the newest on nuget.org (the only source this Mac's settings list); 1.1.5 already carries TR-RAG-001's fix, `ChatMessage.UserWithImages`, though Prompt studio still posts pictures directly. TR-RAG-005 and TR-RAG-006 were checked again on 1.1.5: both are still there and are rewritten against it, each with the smallest program that shows it. TR-RAG-002 to TR-RAG-004 were not part of this check and keep their July text.

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

### TR-RAG-005 — The reference says `UseSqliteVec` takes a connection string, but 1.1.5 takes a file path

Rewritten 2026-11-15 against 1.1.5. Most of the July entry is answered by the XML notes that ship in the package (`TechieRag.xml`): the store needs no native extension (an exact cosine scan in managed code, the same on Mac Catalyst), and it creates two tables, `Documents` and `Chunks`. What is still wrong is the reference document.

- **Severity:** minor
- **Blocks:** no — TrStudio passes a bare file path, which works, and the work carried on
- **Repro:** a `net10.0` console app referencing only TechieRag 1.1.5: `new TechieRagBuilder().UseOllama().UseSqliteVec("Data Source=" + path).Build().ListDocumentsAsync()`, then the same with the bare `path`
- **Expected:** the form the reference shows works: `.UseSqliteVec("Data Source=app.db")` (TechieRag-AI-Reference.md §9 example) and the table row "`UseSqliteVec(connectionString)` — SQLite with vec extension"
- **Actual:** the `Data Source=` form throws `SqliteException: SQLite Error 14: 'unable to open database file'`; the bare path works and creates `Documents` and `Chunks`. The XML note names the argument `databasePath`, "used exactly as given", and no vec extension is loaded
- **Encountered in:** REQ-RAG-007 (character-bible search on the local store), re-check of 15 Nov 2026
- **Workaround:** none needed; TrStudio passes `TrStudio.vectors.db`'s path
- **Suggested fix:** in the reference, name the argument a file path, drop "with vec extension", fix the §9 example, and add the two table names; or accept both forms

### TR-RAG-006 — A SQLite-only desktop app still carries the PostgreSQL, Qdrant and Azure client packages

Rewritten 2026-11-15 against 1.1.5: unchanged.

- **Severity:** minor
- **Blocks:** no — the packages are unused at run time, and the work carried on
- **Repro:** a `net10.0` console app whose only package is TechieRag 1.1.5, then `dotnet list package --include-transitive`
- **Expected:** a project that uses only the SQLite store and an OpenAI-compatible model brings in neither PostgreSQL, Qdrant nor Azure clients
- **Actual:** `Npgsql` 10.0.1, `Pgvector` 0.3.2, `Qdrant.Client` 1.16.1 (with `Grpc.Net.Client`, `Grpc.Core.Api`, `Grpc.Net.Common` 2.71.0) and `Azure.AI.OpenAI` 2.1.0 come in as hard dependencies of `techierag.nuspec` 1.1.5; 8 files, about 4 MB, land in the output folder
- **Encountered in:** REQ-NFR-016 (one desktop app), re-check of 15 Nov 2026
- **Workaround:** none needed; the app is larger than it has to be
- **Suggested fix:** move the stores and clients into `TechieRag.PgVector`, `TechieRag.Qdrant` and `TechieRag.Azure`, as `TechieRag.Embedded` and `TechieRag.Local` already are

