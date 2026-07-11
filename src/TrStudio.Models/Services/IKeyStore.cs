using TrStudio.Models.Entities;

namespace TrStudio.Models.Services;

/// <summary>
/// The encrypted secret store for provider API keys and source tokens (NFR-05).
/// Secrets are encrypted at rest, never logged and never written to URLs; reads for display
/// return masked renderings only.
/// </summary>
public interface IKeyStore
{
    /// <summary>
    /// Encrypts and stores a secret under a name, replacing any existing value.
    /// </summary>
    /// <param name="aName">The secret name (e.g. provider name or source token key).</param>
    /// <param name="aSecret">The raw secret to encrypt and store.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the secret is persisted.</returns>
    Task StoreAsync(string aName, string aSecret, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Decrypts and returns a stored secret for internal provider use.
    /// </summary>
    /// <param name="aName">The secret name.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The raw secret, or null when not stored. Callers must never log the value.</returns>
    Task<string?> RetrieveAsync(string aName, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists masked info for all stored secrets, for admin display.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>Masked key info rows; never raw secrets.</returns>
    Task<IReadOnlyList<ProviderKeyInfo>> ListMaskedAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Deletes a stored secret.
    /// </summary>
    /// <param name="aName">The secret name.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the secret is removed.</returns>
    Task DeleteAsync(string aName, CancellationToken aCancellationToken = default);
}
