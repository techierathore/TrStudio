using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for provider API key metadata (admin-scoped, instance-global). Raw secrets are
/// stored encrypted (NFR-05); this repository handles ciphertext and masked read-models only.
/// </summary>
public interface IProviderKeyRepository
{
    /// <summary>
    /// Lists masked key info for all stored provider keys.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>Masked key info rows; never raw secrets.</returns>
    Task<IReadOnlyList<ProviderKeyInfo>> ListAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Creates or replaces a provider key row.
    /// </summary>
    /// <param name="aProviderName">The provider the key belongs to.</param>
    /// <param name="aEncryptedKey">The encrypted key ciphertext (already encrypted by the key store).</param>
    /// <param name="aIsEnabled">Whether the key is enabled for use.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task UpsertAsync(string aProviderName, string aEncryptedKey, bool aIsEnabled, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Retrieves the encrypted ciphertext for a provider key.
    /// </summary>
    /// <param name="aProviderName">The provider the key belongs to.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The ciphertext, or null when no key is stored.</returns>
    Task<string?> GetEncryptedAsync(string aProviderName, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Enables or disables a stored provider key without touching the ciphertext.
    /// </summary>
    /// <param name="aProviderName">The provider the key belongs to.</param>
    /// <param name="aIsEnabled">The new enabled flag.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> when a row was updated; <c>false</c> when no key is stored for the provider.</returns>
    Task<bool> SetEnabledAsync(string aProviderName, bool aIsEnabled, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Sets the provider's API base address on its existing key row (BRD-93; database-only config).
    /// </summary>
    /// <param name="aProviderName">The provider the key belongs to.</param>
    /// <param name="aBaseUrl">The base address, or null to clear it.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> when a row was updated; <c>false</c> when no key is stored for the provider.</returns>
    Task<bool> SetBaseUrlAsync(string aProviderName, string? aBaseUrl, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Reads the provider's API base address and enabled flag (no secret).
    /// </summary>
    /// <param name="aProviderName">The provider the key belongs to.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The masked-free info row (empty <see cref="ProviderKeyInfo.MaskedKey"/>), or null when none is stored.</returns>
    Task<ProviderKeyInfo?> GetInfoAsync(string aProviderName, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Deletes a provider key row.
    /// </summary>
    /// <param name="aProviderName">The provider the key belongs to.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is removed.</returns>
    Task DeleteAsync(string aProviderName, CancellationToken aCancellationToken = default);
}
