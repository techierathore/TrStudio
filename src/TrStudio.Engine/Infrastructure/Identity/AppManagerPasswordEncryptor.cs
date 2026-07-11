using System.Security.Cryptography;
using System.Text;

namespace TrStudio.Engine.Infrastructure.Identity;

/// <summary>
/// RSA password encryption per the AppManager handshake (guide §2.4): passwords are encrypted
/// with the server's public key using RSA-OAEP-SHA256 and base64-encoded. Plaintext passwords
/// are never sent to AppManager and never persisted by TrStudio.
/// </summary>
public static class AppManagerPasswordEncryptor
{
    /// <summary>
    /// Encrypts a plaintext password with the AppManager RSA public key (RSA-OAEP-SHA256).
    /// </summary>
    /// <param name="aPassword">The plaintext password to encrypt.</param>
    /// <param name="aPublicKeyPem">The PEM-encoded RSA public key from <c>GET /AuthSvc/public-key</c>.</param>
    /// <returns>The base64-encoded RSA ciphertext for the <c>encryptedPassword</c> field.</returns>
    public static string EncryptPassword(string aPassword, string aPublicKeyPem)
    {
        using var vRsa = RSA.Create();
        vRsa.ImportFromPem(aPublicKeyPem);
        var vEncryptedBytes = vRsa.Encrypt(Encoding.UTF8.GetBytes(aPassword), RSAEncryptionPadding.OaepSHA256);
        return Convert.ToBase64String(vEncryptedBytes);
    }

    /// <summary>
    /// Heuristically detects whether a password value is already an RSA ciphertext for the
    /// given key: valid base64 that decodes to exactly the RSA modulus size.
    /// </summary>
    /// <param name="aPassword">The candidate password value.</param>
    /// <param name="aPublicKeyPem">The PEM-encoded RSA public key defining the modulus size.</param>
    /// <returns><c>true</c> when the value already looks like an RSA ciphertext for this key.</returns>
    public static bool IsAlreadyEncrypted(string aPassword, string aPublicKeyPem)
    {
        using var vRsa = RSA.Create();
        vRsa.ImportFromPem(aPublicKeyPem);
        var vKeySizeBytes = vRsa.KeySize / 8;
        var vBuffer = new byte[vKeySizeBytes];
        return Convert.TryFromBase64String(aPassword, vBuffer, out var vBytesWritten)
            && vBytesWritten == vKeySizeBytes;
    }

    /// <summary>
    /// Returns the RSA-encrypted form of a password: values already encrypted by the UI pass
    /// through unchanged; plaintext values are encrypted here so plaintext never reaches AppManager.
    /// </summary>
    /// <param name="aPassword">The password value from the request (plaintext or pre-encrypted).</param>
    /// <param name="aPublicKeyPem">The PEM-encoded RSA public key.</param>
    /// <returns>The base64-encoded RSA ciphertext.</returns>
    public static string EnsureEncrypted(string aPassword, string aPublicKeyPem)
    {
        return IsAlreadyEncrypted(aPassword, aPublicKeyPem)
            ? aPassword
            : EncryptPassword(aPassword, aPublicKeyPem);
    }
}
