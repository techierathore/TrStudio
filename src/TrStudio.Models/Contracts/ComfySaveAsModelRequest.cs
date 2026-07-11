using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for turning an imported + mapped workflow into a registered model
/// (<c>POST /api/v1/admin/comfy/save-as-model</c>, FN-048/BRD-39). The graph is stored server-side
/// as the first workflow version and HIDDEN from the creator-facing registry; only the mapped
/// params are exposed.
/// </summary>
public class ComfySaveAsModelRequest
{
    /// <summary>The registry model id to create (e.g. "my-comfy-portrait").</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>The display name shown in creator model pickers.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>An optional friendly name for the underlying workflow (defaults to the display name).</summary>
    public string? WorkflowName { get; set; }

    /// <summary>The generation task the model performs; defaults to <see cref="GenerationTaskType.WorkflowRun"/>.</summary>
    public GenerationTaskType Task { get; set; } = GenerationTaskType.WorkflowRun;

    /// <summary>The execution tier; defaults to <see cref="GenerationTier.Fast"/> (local MPS).</summary>
    public GenerationTier Tier { get; set; } = GenerationTier.Fast;

    /// <summary>The model licence name recorded on the registry row.</summary>
    public string License { get; set; } = string.Empty;

    /// <summary>The safety class scoping the model's jobs; defaults to fully-synthetic.</summary>
    public SafetyClass SafetyClass { get; set; } = SafetyClass.SyntheticDefault;

    /// <summary>Whether to enable the model for creators on save (it still cannot go LIVE without a passing smoke).</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>The ComfyUI API-format workflow JSON.</summary>
    public string WorkflowJson { get; set; } = string.Empty;

    /// <summary>The input-to-param mappings defining the exposed params.</summary>
    public List<ComfyParamMapping> Mappings { get; set; } = new();
}
