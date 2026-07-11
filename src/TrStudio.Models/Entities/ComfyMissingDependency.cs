namespace TrStudio.Models.Entities;

/// <summary>
/// A workflow dependency the current instance cannot confirm is installed (FN-047/BRD-38): either
/// a custom node class the graph references or a model file a loader node expects. Each carries an
/// actionable install hint.
/// </summary>
public class ComfyMissingDependency
{
    /// <summary>The dependency name — a node class type (custom node) or a model file name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The dependency kind: <c>custom-node</c> or <c>model-file</c>.</summary>
    public string Kind { get; set; } = "custom-node";

    /// <summary>An actionable hint on how to install/obtain the dependency.</summary>
    public string InstallHint { get; set; } = string.Empty;
}
