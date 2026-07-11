namespace TrStudio.Models.Entities;

/// <summary>
/// The result of parsing an imported ComfyUI workflow (FN-043/BRD-34). A well-formed API-format
/// graph yields <see cref="Ok"/> = true with the parsed <see cref="Nodes"/> and the flattened
/// <see cref="ParameterizableInputs"/>; malformed JSON yields <see cref="Ok"/> = false with a
/// clear <see cref="Error"/> (surfaced as a 400, never a 500).
/// </summary>
public class ComfyImportResult
{
    /// <summary>Whether the workflow JSON parsed into a valid API-format node graph.</summary>
    public bool Ok { get; set; }

    /// <summary>The clear parse-error message when <see cref="Ok"/> is false; null otherwise.</summary>
    public string? Error { get; set; }

    /// <summary>The number of nodes parsed out of the graph.</summary>
    public int NodeCount { get; set; }

    /// <summary>The parsed nodes, each with its class type and inputs.</summary>
    public List<ComfyNode> Nodes { get; set; } = new();

    /// <summary>The editable scalar inputs offered for mapping onto registry params.</summary>
    public List<ComfyParameterizableInput> ParameterizableInputs { get; set; } = new();
}
