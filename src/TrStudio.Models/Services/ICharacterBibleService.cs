using TrStudio.Models.Contracts;

namespace TrStudio.Models.Services;

/// <summary>
/// The RAG-backed character bible (RAG-004/BRD-55): per-character persona notes persisted
/// (owner-scoped) and indexed into PgVector via TechieRag, with a script-assist retrieval path that
/// degrades to keyword matching when no embedding model is available.
/// </summary>
public interface ICharacterBibleService
{
    /// <summary>Adds a persona-notes entry to a character's bible and indexes it for retrieval.</summary>
    /// <param name="aCharacterId">The owning character.</param>
    /// <param name="aRequest">The note to add.</param>
    /// <param name="aUserId">The owning AppManager user id (BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The created entry, or null when the character is not found or not owned.</returns>
    Task<CharacterBibleEntryResponse?> AddEntryAsync(Guid aCharacterId, CharacterBibleEntryRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>Lists a character's persisted bible entries, owner-filtered.</summary>
    /// <param name="aCharacterId">The owning character.</param>
    /// <param name="aUserId">The owning AppManager user id (BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The character's entries, or null when the character is not found or not owned.</returns>
    Task<IReadOnlyList<CharacterBibleEntryResponse>?> ListEntriesAsync(Guid aCharacterId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>Deletes a bible entry, owner-filtered.</summary>
    /// <param name="aEntryId">The entry identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> when a row was removed; <c>false</c> otherwise.</returns>
    Task<bool> DeleteEntryAsync(Guid aEntryId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Retrieves the persona-notes entries most relevant to a script-assist query via the vector
    /// path, degrading to a keyword match over the persisted entries when embeddings are unavailable.
    /// </summary>
    /// <param name="aCharacterId">The owning character.</param>
    /// <param name="aQuery">The script-assist query.</param>
    /// <param name="aUserId">The owning AppManager user id (BRD-81).</param>
    /// <param name="aTopK">Maximum entries to return.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The retrieval result, or null when the character is not found or not owned.</returns>
    Task<CharacterBibleRetrievalResponse?> RetrieveAsync(Guid aCharacterId, string aQuery, string aUserId, int aTopK = 5, CancellationToken aCancellationToken = default);
}
