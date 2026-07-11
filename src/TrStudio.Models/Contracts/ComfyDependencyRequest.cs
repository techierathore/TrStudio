namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for scanning a workflow's dependencies
/// (<c>POST /api/v1/admin/comfy/dependencies</c>, FN-047/BRD-38).
/// </summary>
public class ComfyDependencyRequest
{
    /// <summary>The ComfyUI API-format workflow JSON to scan.</summary>
    public string WorkflowJson { get; set; } = string.Empty;
}
