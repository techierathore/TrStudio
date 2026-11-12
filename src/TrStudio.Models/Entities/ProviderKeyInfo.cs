namespace TrStudio.Models.Entities;

/// <summary>
/// Read-model for a stored provider API key. Exposes only a masked rendering of the key —
/// the raw secret never leaves the encrypted key store (NFR-05).
/// </summary>
public class ProviderKeyInfo
{
    /// <summary>The provider the key belongs to (e.g. "HeyGen", "RunPod", "FalAi").</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Whether the key is enabled for use by providers.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Masked rendering of the key for display (e.g. "sk-****abcd"). Never the raw key.</summary>
    public string MaskedKey { get; set; } = string.Empty;

    /// <summary>
    /// The provider's API base address as entered on Registry &amp; providers (e.g. Venice's);
    /// null for providers that need none. Not a secret.
    /// </summary>
    public string? BaseUrl { get; set; }
}
