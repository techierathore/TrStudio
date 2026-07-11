namespace TrStudio.Models.Entities;

/// <summary>
/// The debug bundle for a ComfyUI run (FN-046/BRD-37): the EXACT API-format JSON submitted to
/// ComfyUI's <c>/prompt</c> endpoint, the per-node status list, and detected missing custom
/// nodes/model files with install hints. The submitted JSON and missing-dependency detection are
/// produced without a live ComfyUI; the live per-node status needs a real run.
/// </summary>
public class ComfyDebugData
{
    /// <summary>The exact API-format prompt JSON that is/was submitted to ComfyUI <c>/prompt</c>.</summary>
    public string SubmittedJson { get; set; } = "{}";

    /// <summary>Per-node status for the debug table.</summary>
    public List<ComfyNodeStatus> NodeStatuses { get; set; } = new();

    /// <summary>The workflow dependency scan (missing custom nodes / model files + hints).</summary>
    public ComfyDependencyReport Dependencies { get; set; } = new();

    /// <summary>Whether the run degraded (e.g. no ComfyUI configured) rather than executing live.</summary>
    public bool Degraded { get; set; }

    /// <summary>An actionable message describing the run/degrade state.</summary>
    public string? Message { get; set; }
}
