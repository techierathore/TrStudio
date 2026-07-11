namespace TrStudio.Models.Contracts;

/// <summary>
/// The one-time response when a new API key is issued (REQ-FN-069/BRD-66). The
/// <see cref="PlaintextKey"/> is shown to the owner exactly once and is NOT recoverable afterwards
/// (only its SHA-256 hash is stored, NFR-05).
/// </summary>
public class ApiKeyIssuedResponse
{
    /// <summary>The non-secret metadata for the newly issued key.</summary>
    public ApiKeyResponse Key { get; set; } = new();

    /// <summary>The full plaintext key — displayed once, never retrievable again.</summary>
    public string PlaintextKey { get; set; } = string.Empty;
}
