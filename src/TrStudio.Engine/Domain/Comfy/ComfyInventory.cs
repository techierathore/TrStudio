using System.Text.Json;

namespace TrStudio.Engine.Domain.Comfy;

/// <summary>
/// What a running ComfyUI holds, read from its <c>GET /object_info</c> (REQ-FN-047; owner UAT bug of
/// 16 Nov 2026: every model file a graph named was reported missing because nobody asked ComfyUI). Each
/// key of that answer is a node type ComfyUI can run, built-in or custom; a loader's file input lists
/// the files ComfyUI has for it, either as <c>[["a.safetensors", …], {…}]</c> or, in newer ComfyUI, as
/// <c>["COMBO", {"options": ["a.safetensors", …]}]</c>.
/// </summary>
public sealed class ComfyInventory
{
    private readonly HashSet<string> objNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> objFiles = new(StringComparer.Ordinal);

    /// <summary>The number of node types ComfyUI reported.</summary>
    public int NodeCount => objNodes.Count;

    /// <summary>Whether ComfyUI can run this node type.</summary>
    /// <param name="aClassType">The node's class_type.</param>
    /// <returns>True when ComfyUI listed it.</returns>
    public bool HasNode(string aClassType) => objNodes.Contains(aClassType);

    /// <summary>Whether ComfyUI holds this file for this node's input.</summary>
    /// <param name="aClassType">The loader node's class_type.</param>
    /// <param name="aInput">The input name, e.g. <c>ckpt_name</c>.</param>
    /// <param name="aFile">The file the graph names.</param>
    /// <returns>True when ComfyUI listed the file among that input's choices.</returns>
    public bool HasFile(string aClassType, string aInput, string aFile)
        => objFiles.TryGetValue($"{aClassType}|{aInput}", out var vFiles) && vFiles.Contains(aFile);

    /// <summary>Reads an <c>/object_info</c> answer.</summary>
    /// <param name="aObjectInfoJson">The JSON ComfyUI returned.</param>
    /// <returns>The inventory.</returns>
    public static ComfyInventory Parse(string aObjectInfoJson)
    {
        var vInventory = new ComfyInventory();
        using var vDocument = JsonDocument.Parse(aObjectInfoJson);
        if (vDocument.RootElement.ValueKind != JsonValueKind.Object)
        {
            return vInventory;
        }

        foreach (var vNode in vDocument.RootElement.EnumerateObject())
        {
            vInventory.objNodes.Add(vNode.Name);
            if (vNode.Value.ValueKind != JsonValueKind.Object || !vNode.Value.TryGetProperty("input", out var vInputs)
                || vInputs.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var vGroup in vInputs.EnumerateObject())
            {
                if (vGroup.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var vInput in vGroup.Value.EnumerateObject())
                {
                    var vChoices = Choices(vInput.Value);
                    if (vChoices.Count > 0)
                    {
                        vInventory.objFiles[$"{vNode.Name}|{vInput.Name}"] = vChoices;
                    }
                }
            }
        }

        return vInventory;
    }

    private static HashSet<string> Choices(JsonElement aSpec)
    {
        var vChoices = new HashSet<string>(StringComparer.Ordinal);
        if (aSpec.ValueKind != JsonValueKind.Array || aSpec.GetArrayLength() == 0)
        {
            return vChoices;
        }

        var vFirst = aSpec[0];
        if (vFirst.ValueKind == JsonValueKind.Array)
        {
            AddStrings(vFirst, vChoices);
        }
        else if (vFirst.ValueKind == JsonValueKind.String && vFirst.GetString() == "COMBO" && aSpec.GetArrayLength() > 1
                 && aSpec[1].ValueKind == JsonValueKind.Object && aSpec[1].TryGetProperty("options", out var vOptions))
        {
            AddStrings(vOptions, vChoices);
        }

        return vChoices;
    }

    private static void AddStrings(JsonElement aArray, HashSet<string> aInto)
    {
        if (aArray.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var vItem in aArray.EnumerateArray())
        {
            if (vItem.ValueKind == JsonValueKind.String)
            {
                aInto.Add(vItem.GetString()!);
            }
        }
    }
}
