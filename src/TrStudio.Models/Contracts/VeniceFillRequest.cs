namespace TrStudio.Models.Contracts;

/// <summary>Request body for <c>POST /api/v1/admin/registry/venice/fill</c> (decision 4B, BRD-122).</summary>
public class VeniceFillRequest
{
    /// <summary>The Venice model id to look up in Venice's public model list.</summary>
    public string ProviderModelId { get; set; } = string.Empty;
}
