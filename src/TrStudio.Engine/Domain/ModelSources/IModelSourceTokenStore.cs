namespace TrStudio.Engine.Domain.ModelSources;

/// <summary>
/// Encrypted storage of model-source API tokens (write-only from the API surface; the raw
/// token is only ever read internally to authenticate downloads and is never logged).
/// </summary>
public interface IModelSourceTokenStore
{
    /// <summary>
    /// Encrypts and stores a source's API token, or clears it.
    /// </summary>
    /// <param name="aSourceId">The model source identifier.</param>
    /// <param name="aPlainToken">The raw token to encrypt, or null/empty to clear the stored token.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the encrypted token is persisted.</returns>
    Task SetTokenAsync(Guid aSourceId, string? aPlainToken, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Decrypts and returns a source's API token for internal download authentication.
    /// </summary>
    /// <param name="aSourceId">The model source identifier.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The raw token, or null when none is stored. Callers must never log the value.</returns>
    Task<string?> GetTokenAsync(Guid aSourceId, CancellationToken aCancellationToken = default);
}
