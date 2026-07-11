namespace TrStudio.Models.Entities;

/// <summary>
/// A single parsed node of a ComfyUI API-format workflow graph (FN-043/BRD-34). The node key
/// (e.g. "3") is its <see cref="NodeId"/>; <see cref="ClassType"/> names the ComfyUI node class
/// (e.g. "KSampler"); <see cref="Inputs"/> lists its scalar and link inputs.
/// </summary>
public class ComfyNode
{
    /// <summary>The node's key in the API-format graph (its unique id within the workflow).</summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>The ComfyUI node class type (e.g. "KSampler", "CLIPTextEncode", "SaveImage").</summary>
    public string ClassType { get; set; } = string.Empty;

    /// <summary>The node's inputs, both editable scalars and node-to-node links.</summary>
    public List<ComfyNodeInput> Inputs { get; set; } = new();
}
