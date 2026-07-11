namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for adding a new version to an existing imported workflow
/// (<c>POST /api/v1/admin/comfy/workflows/{id}/versions</c>, FN-049/BRD-40). Adding a version never
/// changes what a live model serves — the model keeps serving its pinned version until re-pinned.
/// </summary>
public class ComfyAddVersionRequest
{
    /// <summary>The new ComfyUI API-format workflow JSON for this version.</summary>
    public string WorkflowJson { get; set; } = string.Empty;

    /// <summary>Optional updated input-to-param mappings for this version.</summary>
    public List<ComfyParamMapping> Mappings { get; set; } = new();
}
