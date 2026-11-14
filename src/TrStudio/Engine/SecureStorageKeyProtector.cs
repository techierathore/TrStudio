using System.Security.Cryptography;
using Microsoft.Maui.Storage;
using TrStudio.Engine.Infrastructure.Security;

namespace TrStudio.Engine;

/// <summary>
/// The operating system's protection for the key store's unlock key (phase 3, ADR-020). The 256-bit
/// key is random, made on first use, and kept through MAUI's <see cref="SecureStorage"/>: a Keychain
/// item on Mac, a Windows data-protection (DPAPI) blob for the signed-in Windows user. The provider
/// keys themselves stay in the local database as ciphertext only.
/// </summary>
public sealed class SecureStorageKeyProtector : IKeyProtector
{
    private const string StorageKey = "trstudio.keystore.unlock.v1";
    private readonly object objGate = new();
    private byte[]? objKey;

    /// <inheritdoc />
    public string Description =>
        OperatingSystem.IsWindows() ? "Windows data protection for your user" : "the macOS Keychain";

    /// <inheritdoc />
    public byte[] GetOrCreateDataKey()
    {
        lock (objGate)
        {
            if (objKey is not null)
            {
                return objKey;
            }

            // SecureStorage is asynchronous; run it off the calling thread so a UI-thread caller cannot deadlock.
            objKey = Task.Run(async () =>
            {
                var vStored = await SecureStorage.Default.GetAsync(StorageKey).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(vStored))
                {
                    return Convert.FromBase64String(vStored);
                }

                var vKey = RandomNumberGenerator.GetBytes(32);
                await SecureStorage.Default.SetAsync(StorageKey, Convert.ToBase64String(vKey)).ConfigureAwait(false);
                return vKey;
            }).GetAwaiter().GetResult();
            return objKey;
        }
    }
}
