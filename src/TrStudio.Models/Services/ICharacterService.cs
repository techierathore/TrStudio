using TrStudio.Models.Contracts;

namespace TrStudio.Models.Services;

/// <summary>
/// Manages synthetic-character personas (FN-036/BRD-27): create/read/update/delete with a LOCKED
/// IDENTITY (seed + reference set), realism→safety-class mapping, and owner scoping (BRD-81). The
/// locked identity is minted once on create and preserved across updates so the persona reproduces
/// across studios.
/// </summary>
public interface ICharacterService
{
    /// <summary>Creates a character and mints its locked identity.</summary>
    /// <param name="aRequest">The persona definition.</param>
    /// <param name="aUserId">The owning AppManager user id (BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The created character.</returns>
    Task<CharacterResponse> CreateAsync(CharacterUpsertRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>Gets a character by id, owner-filtered.</summary>
    /// <param name="aCharacterId">The character identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The character, or null when not found or not owned.</returns>
    Task<CharacterResponse?> GetAsync(Guid aCharacterId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>Lists the caller's characters.</summary>
    /// <param name="aUserId">The owning AppManager user id (BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The caller's characters.</returns>
    Task<IReadOnlyList<CharacterResponse>> ListAsync(string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>Updates a character's mutable fields, preserving the locked identity.</summary>
    /// <param name="aCharacterId">The character identifier.</param>
    /// <param name="aRequest">The updated persona definition.</param>
    /// <param name="aUserId">The owning AppManager user id (BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated character, or null when not found or not owned.</returns>
    Task<CharacterResponse?> UpdateAsync(Guid aCharacterId, CharacterUpsertRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>Deletes a character, owner-filtered.</summary>
    /// <param name="aCharacterId">The character identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> when a row was removed; <c>false</c> when not found or not owned.</returns>
    Task<bool> DeleteAsync(Guid aCharacterId, string aUserId, CancellationToken aCancellationToken = default);
}
