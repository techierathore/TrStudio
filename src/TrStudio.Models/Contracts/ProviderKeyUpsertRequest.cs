namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for storing or updating a provider API key (admin-scoped).
/// </summary>
/// <remarks>
/// <see cref="ApiKey"/> is write-only: it is stored encrypted in the key store (NFR-05)
/// and read endpoints expose only the masked rendering via <see cref="Entities.ProviderKeyInfo"/>.
/// </remarks>
public class ProviderKeyUpsertRequest
{
    /// <summary>The provider the key belongs to (e.g. "HeyGen", "RunPod", "FalAi").</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Write-only raw API key; stored encrypted, never echoed back, never logged.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Whether the key is enabled for use by providers.</summary>
    public bool IsEnabled { get; set; }
}
