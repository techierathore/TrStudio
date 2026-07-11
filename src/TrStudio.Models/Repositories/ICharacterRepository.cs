using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for synthetic-character personas (FN-036). Every read/write applies the
/// <c>"UserId"</c> owner filter server-side so a creator can only see or mutate their own
/// characters (BRD-81).
/// </summary>
public interface ICharacterRepository
{
    /// <summary>
    /// Inserts a new character row. Ownership is recorded in <see cref="Character.UserId"/>.
    /// </summary>
    /// <param name="aCharacter">The character to insert.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertAsync(Character aCharacter, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Gets a character by id, owner-filtered.
    /// </summary>
    /// <param name="aCharacterId">The character identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The character, or null when not found or not owned by <paramref name="aUserId"/>.</returns>
    Task<Character?> GetAsync(Guid aCharacterId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists the user's characters newest-first, owner-filtered.
    /// </summary>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The user's characters; empty when none exist.</returns>
    Task<IReadOnlyList<Character>> ListAsync(string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Updates the mutable fields of a character, owner-filtered. The locked identity
    /// (seed/embedding/references) is preserved by the service layer.
    /// </summary>
    /// <param name="aCharacter">The character carrying updated values.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is updated.</returns>
    Task UpdateAsync(Character aCharacter, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Deletes a character row, owner-filtered.
    /// </summary>
    /// <param name="aCharacterId">The character identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is removed.</returns>
    Task DeleteAsync(Guid aCharacterId, string aUserId, CancellationToken aCancellationToken = default);
}
