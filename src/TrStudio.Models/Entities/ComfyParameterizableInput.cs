namespace TrStudio.Models.Entities;

/// <summary>
/// A single editable scalar input flattened out of a parsed workflow (FN-043/BRD-34) and offered
/// to the admin for mapping onto a registry parameter. Link/internal inputs never appear here.
/// </summary>
public class ComfyParameterizableInput
{
    /// <summary>The stable mapping key for this input, formed as <c>{NodeId}.{InputName}</c> (e.g. "6.text").</summary>
    public string ParamKey { get; set; } = string.Empty;

    /// <summary>The owning node's id.</summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>The owning node's class type (context for the admin choosing what to expose).</summary>
    public string ClassType { get; set; } = string.Empty;

    /// <summary>The input field name within the node.</summary>
    public string InputName { get; set; } = string.Empty;

    /// <summary>The inferred parameter type: <c>text</c>, <c>int</c>, <c>float</c> or <c>bool</c>.</summary>
    public string InferredType { get; set; } = "text";

    /// <summary>The input's current value rendered as a string, offered as the default.</summary>
    public string? DefaultValue { get; set; }
}
