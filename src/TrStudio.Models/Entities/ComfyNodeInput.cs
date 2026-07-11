namespace TrStudio.Models.Entities;

/// <summary>
/// One input field of a parsed ComfyUI node (FN-043/BRD-34). An input is either an editable
/// <c>scalar</c> (a value a creator could set — text/int/float/bool) or a <c>link</c> that wires
/// another node's output into this input; only scalar inputs are parameterizable.
/// </summary>
public class ComfyNodeInput
{
    /// <summary>The input field name as it appears in the node's <c>inputs</c> object (e.g. "seed", "text").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Either <c>scalar</c> (an editable value) or <c>link</c> (a node-to-node connection, hidden from mapping).</summary>
    public string Kind { get; set; } = "scalar";

    /// <summary>The current scalar value rendered as a string, or null for a link input.</summary>
    public string? Value { get; set; }

    /// <summary>The inferred parameter type for a scalar input: <c>text</c>, <c>int</c>, <c>float</c> or <c>bool</c>.</summary>
    public string InferredType { get; set; } = "text";
}
