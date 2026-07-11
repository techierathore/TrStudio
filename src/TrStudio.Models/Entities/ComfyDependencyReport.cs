namespace TrStudio.Models.Entities;

/// <summary>
/// The dependency scan of a workflow (FN-047/BRD-38): which custom nodes and model files the graph
/// requires, and which of those the current instance cannot confirm are installed. Actual
/// installation needs the live ComfyUI environment and is reported here rather than performed.
/// </summary>
public class ComfyDependencyReport
{
    /// <summary>The distinct custom-node class types the graph references (beyond the built-in set).</summary>
    public List<string> RequiredCustomNodes { get; set; } = new();

    /// <summary>The distinct model files the graph's loader nodes expect.</summary>
    public List<string> RequiredModelFiles { get; set; } = new();

    /// <summary>Custom nodes that could not be confirmed installed, with install hints.</summary>
    public List<ComfyMissingDependency> MissingCustomNodes { get; set; } = new();

    /// <summary>Model files that could not be confirmed installed, with install hints.</summary>
    public List<ComfyMissingDependency> MissingModelFiles { get; set; } = new();

    /// <summary>Whether any dependency is missing (a convenience flag for the UI/gate).</summary>
    public bool HasMissing => MissingCustomNodes.Count > 0 || MissingModelFiles.Count > 0;
}
