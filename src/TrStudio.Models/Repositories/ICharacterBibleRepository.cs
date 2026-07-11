using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for character-bible entries (RAG-004). Every read/write applies BOTH the
/// <c>"UserId"</c> owner filter and the <c>"CharacterId"</c> scope so a creator only ever touches
/// their own persona's canon (BRD-81).
/// </summary>
public interface ICharacterBibleRepository
{
    /// <summary>
    /// Inserts a new bible entry.
    /// </summary>
    /// <param name="aEntry">The entry to insert.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertAsync(CharacterBibleEntry aEntry, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists a character's bible entries newest-first, owner + character filtered.
    /// </summary>
    /// <param name="aCharacterId">The owning character.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The character's entries; empty when none exist.</returns>
    Task<IReadOnlyList<CharacterBibleEntry>> ListAsync(Guid aCharacterId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Gets a single bible entry by id, owner-filtered.
    /// </summary>
    /// <param name="aEntryId">The entry identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The entry, or null when not found or not owned.</returns>
    Task<CharacterBibleEntry?> GetAsync(Guid aEntryId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Deletes a bible entry, owner-filtered.
    /// </summary>
    /// <param name="aEntryId">The entry identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is removed.</returns>
    Task DeleteAsync(Guid aEntryId, string aUserId, CancellationToken aCancellationToken = default);
}
