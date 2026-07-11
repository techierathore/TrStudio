using TrStudio.Models.Contracts;

namespace TrStudio.Engine.Domain.ApiKeys;

/// <summary>
/// Owner-scoped lifecycle for external REST API keys (REQ-FN-069/BRD-66) plus the authentication
/// lookup used by the <c>X-Api-Key</c> auth scheme. Keys are hashed at rest; the plaintext is
/// returned exactly once at issue time.
/// </summary>
public interface IApiKeyService
{
    /// <summary>
    /// Issues a new key for a user, returning the one-time plaintext alongside its stored metadata.
    /// </summary>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aName">A creator-facing label for the key.</param>
    /// <param name="aScope">The scope to grant, or null for the default (<c>jobs</c>).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The issued key (metadata + one-time plaintext).</returns>
    Task<ApiKeyIssuedResponse> IssueAsync(string aUserId, string aName, string? aScope, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists a user's keys (non-secret metadata only), owner-filtered.
    /// </summary>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The user's keys.</returns>
    Task<IReadOnlyList<ApiKeyResponse>> ListAsync(string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Revokes a key, owner-filtered.
    /// </summary>
    /// <param name="aApiKeyId">The key identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> when a key was revoked; <c>false</c> when not found or not owned.</returns>
    Task<bool> RevokeAsync(Guid aApiKeyId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Authenticates a presented plaintext key: hashes it, resolves an active row and records the
    /// use. Never throws for a bad key — returns null so the auth handler can fail the request.
    /// </summary>
    /// <param name="aPresentedKey">The raw key from the <c>X-Api-Key</c> header.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The resolved principal data, or null when the key is unknown/revoked/blank.</returns>
    Task<ApiKeyAuthResult?> AuthenticateAsync(string? aPresentedKey, CancellationToken aCancellationToken = default);
}

/// <summary>The identity resolved from a valid API key (REQ-FN-069).</summary>
/// <param name="UserId">The AppManager user id the key authenticates as.</param>
/// <param name="Scope">The scope granted to the key.</param>
/// <param name="ApiKeyId">The identifier of the matched key.</param>
public sealed record ApiKeyAuthResult(string UserId, string Scope, Guid ApiKeyId);
