namespace TrStudio.Models.Entities;

/// <summary>
/// One immutable version of an imported ComfyUI workflow (FN-049/BRD-40). Each version carries the
/// API-format graph JSON, the mapped registry params for that graph, and a sandbox-smoke verdict
/// (<see cref="SmokeStatus"/>). A registered model serves whichever version is pinned by its
/// <see cref="RegistryModel.WorkflowRef"/>, so newer versions can exist without changing what a
/// live model serves until it is re-pinned.
/// </summary>
public class ComfyWorkflowVersion
{
    /// <summary>Stable identifier for this workflow version (referenced by <see cref="RegistryModel.WorkflowRef"/>).</summary>
    public Guid VersionId { get; set; }

    /// <summary>The parent workflow this version belongs to.</summary>
    public Guid WorkflowId { get; set; }

    /// <summary>Monotonic version number within the workflow (1, 2, 3, ...).</summary>
    public int VersionNumber { get; set; }

    /// <summary>The full ComfyUI API-format graph JSON — stored server-side and HIDDEN from creators.</summary>
    public string GraphJson { get; set; } = "{}";

    /// <summary>The mapped registry parameter specs for this version, serialised as JSON.</summary>
    public string MappedParamsJson { get; set; } = "[]";

    /// <summary>The sandbox-smoke verdict gating go-live: <c>None</c>, <c>Passed</c>, <c>Failed</c> or <c>Degraded</c>.</summary>
    public string SmokeStatus { get; set; } = "None";

    /// <summary>The last smoke run's actionable message, if any.</summary>
    public string? SmokeMessage { get; set; }

    /// <summary>UTC timestamp when this version was created.</summary>
    public DateTime CreatedUtc { get; set; }
}
