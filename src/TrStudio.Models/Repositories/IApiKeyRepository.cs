using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for external API keys (REQ-FN-069/BRD-66). Owner-scoped surfaces (list/revoke) apply
/// the <c>"UserId"</c> owner filter (BRD-81); the authentication lookup is by key hash and is
/// necessarily global (the caller has not yet been identified). Only key hashes are stored.
/// </summary>
public interface IApiKeyRepository
{
    /// <summary>
    /// Inserts a newly issued key row (only its hash is persisted).
    /// </summary>
    /// <param name="aApiKey">The key metadata + hash to persist.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertAsync(ApiKey aApiKey, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Looks up an active (non-revoked) key by its SHA-256 hash — the authentication path.
    /// </summary>
    /// <param name="aKeyHash">The lowercase hex SHA-256 hash of the presented key.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The matching active key, or null when absent or revoked.</returns>
    Task<ApiKey?> GetActiveByHashAsync(string aKeyHash, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists a user's keys newest-first (including revoked ones for the audit view), owner-filtered.
    /// </summary>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The user's keys.</returns>
    Task<IReadOnlyList<ApiKey>> ListByUserAsync(string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Records that a key was just used (updates <see cref="ApiKey.LastUsedUtc"/>).
    /// </summary>
    /// <param name="aApiKeyId">The key identifier.</param>
    /// <param name="aUsedUtc">The use instant.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the touch is applied.</returns>
    Task TouchUsedAsync(Guid aApiKeyId, DateTime aUsedUtc, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Revokes a key, owner-filtered so a user can only revoke their own keys.
    /// </summary>
    /// <param name="aApiKeyId">The key identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> when a row was revoked; <c>false</c> when not found or not owned.</returns>
    Task<bool> RevokeAsync(Guid aApiKeyId, string aUserId, CancellationToken aCancellationToken = default);
}
