namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for pinning a registered model to a known-good workflow version
/// (<c>POST /api/v1/admin/comfy/models/{modelId}/pin</c>, FN-049/BRD-40).
/// </summary>
public class ComfyPinRequest
{
    /// <summary>The workflow version to pin as the served version for the model.</summary>
    public Guid VersionId { get; set; }
}
