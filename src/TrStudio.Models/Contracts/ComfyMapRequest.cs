namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for mapping workflow inputs to registry params
/// (<c>POST /api/v1/admin/comfy/map</c>, FN-044/BRD-35).
/// </summary>
public class ComfyMapRequest
{
    /// <summary>The ComfyUI API-format workflow JSON the mappings refer to.</summary>
    public string WorkflowJson { get; set; } = string.Empty;

    /// <summary>The admin-chosen input-to-param mappings.</summary>
    public List<ComfyParamMapping> Mappings { get; set; } = new();
}
