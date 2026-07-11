namespace TrStudio.Engine.Domain.Setup;

/// <summary>
/// Symmetric encryption seam for setup-cluster secrets (model-source API tokens).
/// Implementations encrypt at rest with AES-256-GCM (NFR-05: never plain text, never logged).
/// </summary>
public interface ISetupSecretProtector
{
    /// <summary>
    /// Encrypts a secret for storage.
    /// </summary>
    /// <param name="aPlainText">The raw secret.</param>
    /// <returns>The base64 ciphertext envelope (nonce + cipher + tag).</returns>
    string Protect(string aPlainText);

    /// <summary>
    /// Decrypts a stored secret.
    /// </summary>
    /// <param name="aProtectedText">The base64 ciphertext envelope produced by <see cref="Protect"/>.</param>
    /// <returns>The raw secret.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">The envelope is invalid or the key changed.</exception>
    string Unprotect(string aProtectedText);
}
