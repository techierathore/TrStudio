using Microsoft.Extensions.Logging;
using TechieRag;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Characters;

/// <summary>
/// Default <see cref="ICharacterBibleService"/> (RAG-004/BRD-55). Persona notes are persisted as the
/// durable, owner-scoped source of truth (Dapper) AND indexed into PgVector via TechieRag so
/// script-assist can retrieve a character's canon. Every TechieRag interaction is best-effort and
/// wrapped: when no embedding model is available (e.g. offline dev), ingestion is a no-op and
/// retrieval degrades to a deterministic keyword match over the persisted entries — the persistence
/// and the retrieval CONTRACT always hold. TechieRag gaps are recorded in
/// <c>docs/TrStudio-TechieRag-Feedback.md</c>.
/// </summary>
public class CharacterBibleService : ICharacterBibleService
{
    private readonly ICharacterBibleRepository objBibleRepository;
    private readonly ICharacterRepository objCharacterRepository;
    private readonly ITechieRag objRag;
    private readonly ILogger<CharacterBibleService> objLogger;

    /// <summary>
    /// Initialises the service.
    /// </summary>
    /// <param name="aBibleRepository">Owner + character scoped bible persistence (BRD-81).</param>
    /// <param name="aCharacterRepository">Owner-filtered character lookup (ownership gate).</param>
    /// <param name="aRag">The TechieRag client (embedding + PgVector; may be offline).</param>
    /// <param name="aLogger">Structured logger.</param>
    public CharacterBibleService(
        ICharacterBibleRepository aBibleRepository,
        ICharacterRepository aCharacterRepository,
        ITechieRag aRag,
        ILogger<CharacterBibleService> aLogger)
    {
        objBibleRepository = aBibleRepository;
        objCharacterRepository = aCharacterRepository;
        objRag = aRag;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<CharacterBibleEntryResponse?> AddEntryAsync(Guid aCharacterId, CharacterBibleEntryRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vCharacter = await objCharacterRepository.GetAsync(aCharacterId, aUserId, aCancellationToken).ConfigureAwait(false);
        if (vCharacter is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(aRequest.Content))
        {
            throw new ArgumentException("Bible entry content is required.");
        }

        var vNow = DateTime.UtcNow;
        var vEntry = new CharacterBibleEntry
        {
            EntryId = Guid.NewGuid(),
            CharacterId = aCharacterId,
            UserId = aUserId,
            Title = string.IsNullOrWhiteSpace(aRequest.Title) ? "Note" : aRequest.Title.Trim(),
            Content = aRequest.Content.Trim(),
            CreatedUtc = vNow,
            UpdatedUtc = vNow
        };

        // Durable source of truth first — this NEVER depends on an embedding model being available.
        await objBibleRepository.InsertAsync(vEntry, aCancellationToken).ConfigureAwait(false);

        // Best-effort vector indexing; a missing embedding model must not fail the write (degrade).
        await TryIndexAsync(vEntry, aCancellationToken).ConfigureAwait(false);

        return ToResponse(vEntry);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CharacterBibleEntryResponse>?> ListEntriesAsync(Guid aCharacterId, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vCharacter = await objCharacterRepository.GetAsync(aCharacterId, aUserId, aCancellationToken).ConfigureAwait(false);
        if (vCharacter is null)
        {
            return null;
        }

        var vEntries = await objBibleRepository.ListAsync(aCharacterId, aUserId, aCancellationToken).ConfigureAwait(false);
        return vEntries.Select(ToResponse).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> DeleteEntryAsync(Guid aEntryId, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vEntry = await objBibleRepository.GetAsync(aEntryId, aUserId, aCancellationToken).ConfigureAwait(false);
        if (vEntry is null)
        {
            return false;
        }

        await objBibleRepository.DeleteAsync(aEntryId, aUserId, aCancellationToken).ConfigureAwait(false);
        await TryDeleteIndexAsync(vEntry, aCancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<CharacterBibleRetrievalResponse?> RetrieveAsync(Guid aCharacterId, string aQuery, string aUserId, int aTopK = 5, CancellationToken aCancellationToken = default)
    {
        var vCharacter = await objCharacterRepository.GetAsync(aCharacterId, aUserId, aCancellationToken).ConfigureAwait(false);
        if (vCharacter is null)
        {
            return null;
        }

        var vTopK = Math.Clamp(aTopK, 1, 25);
        var vEntries = await objBibleRepository.ListAsync(aCharacterId, aUserId, aCancellationToken).ConfigureAwait(false);
        var vResponse = new CharacterBibleRetrievalResponse { CharacterId = aCharacterId, Query = aQuery ?? string.Empty };

        // ---- Vector path (TechieRag/PgVector). Scoped to this character's owned entries. ----
        var vVectorHits = await TryVectorSearchAsync(aCharacterId, aQuery, vTopK, vEntries, aCancellationToken).ConfigureAwait(false);
        if (vVectorHits is { Count: > 0 })
        {
            vResponse.Mode = "techierag-vector";
            vResponse.Degraded = false;
            vResponse.Entries = vVectorHits.Select(ToResponse).ToList();
            return vResponse;
        }

        // ---- Keyword fallback over the persisted entries (no embedding model / empty index). ----
        vResponse.Mode = "keyword-fallback";
        vResponse.Degraded = true;
        vResponse.Message =
            "Vector retrieval unavailable (no embedding model / empty index) — degraded to a deterministic keyword match. " +
            "Configure TechieRag embeddings (e.g. Ollama bge-m3) + PgVector to enable semantic script-assist.";
        vResponse.Entries = KeywordRank(vEntries, aQuery, vTopK).Select(ToResponse).ToList();
        return vResponse;
    }

    private async Task TryIndexAsync(CharacterBibleEntry aEntry, CancellationToken aCancellationToken)
    {
        try
        {
            await objRag.InitializeAsync(aCancellationToken).ConfigureAwait(false);
            await objRag.IngestTextAsync(
                $"{aEntry.Title}\n{aEntry.Content}",
                DocumentId(aEntry.CharacterId, aEntry.EntryId),
                new Dictionary<string, object>
                {
                    ["characterId"] = aEntry.CharacterId.ToString(),
                    ["userId"] = aEntry.UserId,
                    ["entryId"] = aEntry.EntryId.ToString(),
                    ["title"] = aEntry.Title
                },
                aCancellationToken).ConfigureAwait(false);
        }
        catch (Exception vException)
        {
            // Expected when no embedding model is configured; the persisted entry is unaffected.
            objLogger.LogInformation(vException,
                "Character-bible vector indexing degraded for entry {EntryId} (no embedding model); entry persisted regardless (RAG-004)",
                aEntry.EntryId);
        }
    }

    private async Task TryDeleteIndexAsync(CharacterBibleEntry aEntry, CancellationToken aCancellationToken)
    {
        try
        {
            await objRag.DeleteDocumentAsync(DocumentId(aEntry.CharacterId, aEntry.EntryId), aCancellationToken).ConfigureAwait(false);
        }
        catch (Exception vException)
        {
            objLogger.LogInformation(vException, "Character-bible vector delete degraded for entry {EntryId}", aEntry.EntryId);
        }
    }

    /// <summary>
    /// Runs the TechieRag vector search and maps the hits back to this character's OWNED persisted
    /// entries (owner + character scope re-applied in-process, BRD-81). Returns null on any failure so
    /// the caller degrades to keyword matching.
    /// </summary>
    private async Task<List<CharacterBibleEntry>?> TryVectorSearchAsync(
        Guid aCharacterId,
        string aQuery,
        int aTopK,
        IReadOnlyList<CharacterBibleEntry> aOwnedEntries,
        CancellationToken aCancellationToken)
    {
        if (string.IsNullOrWhiteSpace(aQuery) || aOwnedEntries.Count == 0)
        {
            return null;
        }

        try
        {
            await objRag.InitializeAsync(aCancellationToken).ConfigureAwait(false);
            var vResults = await objRag.SearchAsync(aQuery, aTopK, null, aCancellationToken).ConfigureAwait(false);
            if (vResults is null || vResults.Count == 0)
            {
                return null;
            }

            // Map vector hits → owned entries by the entry-id metadata; drop anything not owned by this
            // character (defence-in-depth over the vector store's own filtering).
            var vOwnedById = aOwnedEntries.ToDictionary(aEntry => aEntry.EntryId);
            var vHits = new List<CharacterBibleEntry>();
            foreach (var vResult in vResults)
            {
                if (vResult.Chunk?.Metadata is { } vMetadata
                    && vMetadata.TryGetValue("entryId", out var vEntryId)
                    && Guid.TryParse(vEntryId?.ToString(), out var vGuid)
                    && vOwnedById.TryGetValue(vGuid, out var vEntry)
                    && vEntry.CharacterId == aCharacterId
                    && vHits.All(aExisting => aExisting.EntryId != vGuid))
                {
                    vHits.Add(vEntry);
                }
            }

            return vHits.Count > 0 ? vHits : null;
        }
        catch (Exception vException)
        {
            objLogger.LogInformation(vException,
                "Character-bible vector retrieval degraded for character {CharacterId} (no embedding model); using keyword fallback (RAG-004)",
                aCharacterId);
            return null;
        }
    }

    /// <summary>
    /// Deterministic keyword ranking over the persisted entries: scores each entry by how many of the
    /// query's distinct word tokens appear in its title/content, newest-first as the tiebreak.
    /// </summary>
    private static List<CharacterBibleEntry> KeywordRank(IReadOnlyList<CharacterBibleEntry> aEntries, string aQuery, int aTopK)
    {
        var vTokens = Tokenize(aQuery);
        if (vTokens.Count == 0)
        {
            return aEntries.OrderByDescending(aEntry => aEntry.CreatedUtc).Take(aTopK).ToList();
        }

        return aEntries
            .Select(aEntry => new
            {
                Entry = aEntry,
                Score = vTokens.Count(aToken => (aEntry.Title + " " + aEntry.Content).Contains(aToken, StringComparison.OrdinalIgnoreCase))
            })
            .Where(aScored => aScored.Score > 0)
            .OrderByDescending(aScored => aScored.Score)
            .ThenByDescending(aScored => aScored.Entry.CreatedUtc)
            .Take(aTopK)
            .Select(aScored => aScored.Entry)
            .ToList();
    }

    private static HashSet<string> Tokenize(string aText)
        => string.IsNullOrWhiteSpace(aText)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : aText
                .Split(new[] { ' ', ',', '.', '!', '?', ';', ':', '\n', '\t', '"', '\'' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(aToken => aToken.Length > 2)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string DocumentId(Guid aCharacterId, Guid aEntryId) => $"bible:{aCharacterId}:{aEntryId}";

    private static CharacterBibleEntryResponse ToResponse(CharacterBibleEntry aEntry) => new()
    {
        EntryId = aEntry.EntryId,
        CharacterId = aEntry.CharacterId,
        Title = aEntry.Title,
        Content = aEntry.Content,
        CreatedUtc = aEntry.CreatedUtc
    };
}
