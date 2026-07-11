namespace TrStudio.Models.Entities;

/// <summary>
/// Per-node execution status for the debug view (FN-046/BRD-37). Live status/errors come from the
/// ComfyUI WebSocket during a real run; before/without a run every node reports <c>pending</c>.
/// </summary>
public class ComfyNodeStatus
{
    /// <summary>The node id this status refers to.</summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>The node class type.</summary>
    public string ClassType { get; set; } = string.Empty;

    /// <summary>The node state: <c>pending</c>, <c>running</c>, <c>completed</c> or <c>error</c>.</summary>
    public string Status { get; set; } = "pending";

    /// <summary>The node error message when <see cref="Status"/> is <c>error</c>; null otherwise.</summary>
    public string? Error { get; set; }
}
