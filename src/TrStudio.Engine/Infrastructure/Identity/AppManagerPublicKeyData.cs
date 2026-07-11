namespace TrStudio.Engine.Infrastructure.Identity;

/// <summary>
/// Wire payload of <c>GET /AuthSvc/public-key</c>.
/// </summary>
internal sealed class AppManagerPublicKeyData
{
    /// <summary>The PEM-encoded RSA public key for password encryption.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>The encryption algorithm label (expected: "RSA-OAEP-256").</summary>
    public string? Algorithm { get; set; }
}
