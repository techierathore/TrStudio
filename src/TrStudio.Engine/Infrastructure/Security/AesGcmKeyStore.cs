using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Infrastructure.Security;

/// <summary>
/// AES-GCM implementation of <see cref="IKeyStore"/> (REQ-FN-007, NFR-05). The 256-bit data
/// key is derived (SHA-256) from the <c>Security:KeyStoreKey</c> configuration value; each
/// secret is sealed with a fresh 96-bit nonce and stored as
/// <c>base64(nonce || tag || ciphertext)</c> in the <c>"ProviderKey"</c> table. Raw secrets
/// are never logged and never returned by listing APIs — only masked renderings.
/// </summary>
public class AesGcmKeyStore : IKeyStore
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly IProviderKeyRepository objRepository;
    private readonly IConfiguration objConfiguration;

    /// <summary>
    /// Initialises the key store.
    /// </summary>
    /// <param name="aRepository">Repository persisting the ciphertext rows.</param>
    /// <param name="aConfiguration">Configuration carrying <c>Security:KeyStoreKey</c>.</param>
    public AesGcmKeyStore(IProviderKeyRepository aRepository, IConfiguration aConfiguration)
    {
        objRepository = aRepository;
        objConfiguration = aConfiguration;
    }

    /// <inheritdoc />
    public async Task StoreAsync(string aName, string aSecret, CancellationToken aCancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aName))
        {
            throw new ArgumentException("Secret name is required.", nameof(aName));
        }

        if (string.IsNullOrEmpty(aSecret))
        {
            throw new ArgumentException("Secret value is required.", nameof(aSecret));
        }

        var vExisting = await objRepository.ListAsync(aCancellationToken).ConfigureAwait(false);
        var vIsEnabled = vExisting.FirstOrDefault(aInfo => aInfo.ProviderName == aName)?.IsEnabled ?? true;
        var vCiphertext = Encrypt(aSecret);
        await objRepository.UpsertAsync(aName, vCiphertext, vIsEnabled, aCancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string?> RetrieveAsync(string aName, CancellationToken aCancellationToken = default)
    {
        var vCiphertext = await objRepository.GetEncryptedAsync(aName, aCancellationToken).ConfigureAwait(false);
        return vCiphertext is null ? null : Decrypt(vCiphertext);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProviderKeyInfo>> ListMaskedAsync(CancellationToken aCancellationToken = default)
    {
        var vRows = await objRepository.ListAsync(aCancellationToken).ConfigureAwait(false);
        var vResult = new List<ProviderKeyInfo>(vRows.Count);
        foreach (var vRow in vRows)
        {
            var vCiphertext = await objRepository.GetEncryptedAsync(vRow.ProviderName, aCancellationToken).ConfigureAwait(false);
            var vMask = vCiphertext is null ? "****" : Mask(Decrypt(vCiphertext));
            vResult.Add(new ProviderKeyInfo
            {
                ProviderName = vRow.ProviderName,
                IsEnabled = vRow.IsEnabled,
                MaskedKey = vMask
            });
        }

        return vResult;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string aName, CancellationToken aCancellationToken = default)
        => objRepository.DeleteAsync(aName, aCancellationToken);

    /// <summary>
    /// Produces the display mask for a secret: the last four characters preceded by asterisks
    /// (e.g. <c>"****abcd"</c>); secrets of four characters or fewer render as <c>"****"</c>.
    /// </summary>
    /// <param name="aSecret">The raw secret to mask; never logged.</param>
    /// <returns>The masked rendering, safe for UI and logs.</returns>
    public static string Mask(string aSecret)
        => aSecret.Length <= 4 ? "****" : "****" + aSecret[^4..];

    private string Encrypt(string aSecret)
    {
        var vKey = DeriveKey();
        var vNonce = RandomNumberGenerator.GetBytes(NonceSize);
        var vPlain = Encoding.UTF8.GetBytes(aSecret);
        var vCipher = new byte[vPlain.Length];
        var vTag = new byte[TagSize];

        using var vAes = new AesGcm(vKey, TagSize);
        vAes.Encrypt(vNonce, vPlain, vCipher, vTag);

        var vPayload = new byte[NonceSize + TagSize + vCipher.Length];
        Buffer.BlockCopy(vNonce, 0, vPayload, 0, NonceSize);
        Buffer.BlockCopy(vTag, 0, vPayload, NonceSize, TagSize);
        Buffer.BlockCopy(vCipher, 0, vPayload, NonceSize + TagSize, vCipher.Length);
        return Convert.ToBase64String(vPayload);
    }

    private string Decrypt(string aPayloadBase64)
    {
        var vKey = DeriveKey();
        var vPayload = Convert.FromBase64String(aPayloadBase64);
        var vNonce = vPayload.AsSpan(0, NonceSize).ToArray();
        var vTag = vPayload.AsSpan(NonceSize, TagSize).ToArray();
        var vCipher = vPayload.AsSpan(NonceSize + TagSize).ToArray();
        var vPlain = new byte[vCipher.Length];

        using var vAes = new AesGcm(vKey, TagSize);
        vAes.Decrypt(vNonce, vCipher, vTag, vPlain);
        return Encoding.UTF8.GetString(vPlain);
    }

    private byte[] DeriveKey()
    {
        var vKeyMaterial = objConfiguration["Security:KeyStoreKey"];
        if (string.IsNullOrWhiteSpace(vKeyMaterial))
        {
            throw new InvalidOperationException(
                "Security:KeyStoreKey is not configured — set it before storing provider keys " +
                "(a dev default ships in appsettings.Development.json only).");
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(vKeyMaterial));
    }
}
