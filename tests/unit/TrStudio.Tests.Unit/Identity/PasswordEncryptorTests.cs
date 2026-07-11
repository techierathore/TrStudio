using System.Security.Cryptography;
using System.Text;
using TrStudio.Engine.Infrastructure.Identity;

namespace TrStudio.Tests.Unit.Identity;

/// <summary>
/// Tests for <see cref="AppManagerPasswordEncryptor"/>: the RSA-OAEP-SHA256 password
/// encryption mandated by the AppManager handshake (guide §2.4).
/// </summary>
public class PasswordEncryptorTests
{
    /// <summary>
    /// Encrypting a password with a generated public key produces base64 ciphertext that the
    /// matching private key decrypts back to the original password using OAEP-SHA256 padding —
    /// proving the algorithm matches the guide exactly.
    /// </summary>
    [Fact]
    public void EncryptRoundTripsWithPrivateKey()
    {
        using var vRsa = RSA.Create(2048);
        var vPublicKeyPem = vRsa.ExportSubjectPublicKeyInfoPem();
        const string Password = "Sup3r$ecretPass!";

        var vCiphertext = AppManagerPasswordEncryptor.EncryptPassword(Password, vPublicKeyPem);

        var vDecrypted = vRsa.Decrypt(Convert.FromBase64String(vCiphertext), RSAEncryptionPadding.OaepSHA256);
        Assert.Equal(Password, Encoding.UTF8.GetString(vDecrypted));
    }

    /// <summary>
    /// The ciphertext is valid base64 whose decoded length equals the RSA modulus size
    /// (256 bytes for a 2048-bit key), the wire shape AppManager expects.
    /// </summary>
    [Fact]
    public void EncryptProducesModulusSizedBase64()
    {
        using var vRsa = RSA.Create(2048);
        var vPublicKeyPem = vRsa.ExportSubjectPublicKeyInfoPem();

        var vCiphertext = AppManagerPasswordEncryptor.EncryptPassword("AnyPass@123", vPublicKeyPem);

        Assert.Equal(256, Convert.FromBase64String(vCiphertext).Length);
    }

    /// <summary>
    /// EnsureEncrypted passes an already-encrypted value through unchanged (UI-side encryption
    /// per the LoginRequest contract) so the password is never double-encrypted.
    /// </summary>
    [Fact]
    public void EnsureEncryptedPassesCiphertextThrough()
    {
        using var vRsa = RSA.Create(2048);
        var vPublicKeyPem = vRsa.ExportSubjectPublicKeyInfoPem();
        var vCiphertext = AppManagerPasswordEncryptor.EncryptPassword("MyPass@123", vPublicKeyPem);

        var vResult = AppManagerPasswordEncryptor.EnsureEncrypted(vCiphertext, vPublicKeyPem);

        Assert.Equal(vCiphertext, vResult);
    }

    /// <summary>
    /// EnsureEncrypted encrypts a plaintext password so plaintext never reaches AppManager even
    /// when a caller skipped client-side encryption.
    /// </summary>
    [Fact]
    public void EnsureEncryptedEncryptsPlaintext()
    {
        using var vRsa = RSA.Create(2048);
        var vPublicKeyPem = vRsa.ExportSubjectPublicKeyInfoPem();
        const string Password = "PlainText@123";

        var vResult = AppManagerPasswordEncryptor.EnsureEncrypted(Password, vPublicKeyPem);

        Assert.NotEqual(Password, vResult);
        var vDecrypted = vRsa.Decrypt(Convert.FromBase64String(vResult), RSAEncryptionPadding.OaepSHA256);
        Assert.Equal(Password, Encoding.UTF8.GetString(vDecrypted));
    }
}
