namespace TrStudio.Models.Contracts;

/// <summary>
/// Result of <c>POST /api/v1/admin/providers/keys/{aProviderName}/test</c> (BRD-95): either
/// <c>Connected</c> or the provider's own error text, word for word.
/// </summary>
public class ProviderTestConnectionResponse
{
    /// <summary>The provider tested (e.g. "Venice").</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Whether the stored address and key were accepted.</summary>
    public bool Connected { get; set; }

    /// <summary>The HTTP status the provider returned, when a call was made.</summary>
    public int? HttpStatus { get; set; }

    /// <summary>"Connected", or the provider's own error text (e.g. "Venice: Authentication failed (HTTP 401)").</summary>
    public string Message { get; set; } = string.Empty;
}
