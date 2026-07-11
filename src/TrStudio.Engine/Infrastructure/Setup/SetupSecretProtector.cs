using System.Security.Cryptography;
using System.Text;
using TrStudio.Engine.Domain.Setup;

namespace TrStudio.Engine.Infrastructure.Setup;

/// <summary>
/// AES-256-GCM <see cref="ISetupSecretProtector"/>. The key comes from
/// <c>Setup:TokenEncryptionKey</c> (base64, 32 bytes) when configured; otherwise a random key
/// is generated once and persisted under the managed root (<c>.keys/token.key</c>) so tokens
/// survive restarts without ever being stored in plain text (NFR-05).
/// </summary>
public class SetupSecretProtector : ISetupSecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    private readonly Lazy<byte[]> objKey;

    /// <summary>
    /// Initialises the protector; key material is resolved lazily on first use.
    /// </summary>
    /// <param name="aOptions">Setup options, possibly carrying an explicit base64 key.</param>
    /// <param name="aPaths">Managed-environment paths for the generated key file.</param>
    public SetupSecretProtector(SetupOptions aOptions, SetupPaths aPaths)
    {
        objKey = new Lazy<byte[]>(() => ResolveKey(aOptions, aPaths));
    }

    /// <inheritdoc />
    public string Protect(string aPlainText)
    {
        var vPlainBytes = Encoding.UTF8.GetBytes(aPlainText);
        var vNonce = RandomNumberGenerator.GetBytes(NonceSize);
        var vCipher = new byte[vPlainBytes.Length];
        var vTag = new byte[TagSize];

        using var vAes = new AesGcm(objKey.Value, TagSize);
        vAes.Encrypt(vNonce, vPlainBytes, vCipher, vTag);

        var vEnvelope = new byte[NonceSize + vCipher.Length + TagSize];
        vNonce.CopyTo(vEnvelope, 0);
        vCipher.CopyTo(vEnvelope, NonceSize);
        vTag.CopyTo(vEnvelope, NonceSize + vCipher.Length);
        return Convert.ToBase64String(vEnvelope);
    }

    /// <inheritdoc />
    public string Unprotect(string aProtectedText)
    {
        var vEnvelope = Convert.FromBase64String(aProtectedText);
        var vNonce = vEnvelope.AsSpan(0, NonceSize);
        var vTag = vEnvelope.AsSpan(vEnvelope.Length - TagSize, TagSize);
        var vCipher = vEnvelope.AsSpan(NonceSize, vEnvelope.Length - NonceSize - TagSize);
        var vPlain = new byte[vCipher.Length];

        using var vAes = new AesGcm(objKey.Value, TagSize);
        vAes.Decrypt(vNonce, vCipher, vTag, vPlain);
        return Encoding.UTF8.GetString(vPlain);
    }

    private static byte[] ResolveKey(SetupOptions aOptions, SetupPaths aPaths)
    {
        if (!string.IsNullOrWhiteSpace(aOptions.TokenEncryptionKey))
        {
            var vConfigured = Convert.FromBase64String(aOptions.TokenEncryptionKey);
            if (vConfigured.Length != KeySize)
            {
                throw new InvalidOperationException("Setup:TokenEncryptionKey must be a base64-encoded 32-byte key.");
            }

            return vConfigured;
        }

        var vKeyFile = Path.Combine(aPaths.KeysDir, "token.key");
        if (File.Exists(vKeyFile))
        {
            return File.ReadAllBytes(vKeyFile);
        }

        Directory.CreateDirectory(aPaths.KeysDir);
        var vKey = RandomNumberGenerator.GetBytes(KeySize);
        File.WriteAllBytes(vKeyFile, vKey);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(vKeyFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return vKey;
    }
}
