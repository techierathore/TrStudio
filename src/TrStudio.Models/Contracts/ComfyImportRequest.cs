namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for importing/parsing a ComfyUI workflow
/// (<c>POST /api/v1/admin/comfy/import</c>, FN-043/BRD-34). The workflow JSON is the API-format
/// graph pasted or drag-dropped by the admin.
/// </summary>
public class ComfyImportRequest
{
    /// <summary>An optional friendly name for the imported workflow.</summary>
    public string? Name { get; set; }

    /// <summary>The ComfyUI API-format workflow JSON to parse.</summary>
    public string WorkflowJson { get; set; } = string.Empty;
}
