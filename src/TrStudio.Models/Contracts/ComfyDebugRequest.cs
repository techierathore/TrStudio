namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for building a workflow debug bundle
/// (<c>POST /api/v1/admin/comfy/debug</c>, FN-046/BRD-37): the exact submitted JSON plus missing
/// dependency detection for the given graph and (optional) parameter values.
/// </summary>
public class ComfyDebugRequest
{
    /// <summary>The ComfyUI API-format workflow JSON.</summary>
    public string WorkflowJson { get; set; } = string.Empty;

    /// <summary>Optional parameter values keyed by <c>{NodeId}.{InputName}</c> to substitute into the graph.</summary>
    public string? ParamsJson { get; set; }
}
