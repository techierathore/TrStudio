using System.Globalization;
using System.Text.Json;
using TrStudio.Models.Entities;

namespace TrStudio.Engine.Domain.Comfy;

/// <summary>
/// Parses a ComfyUI API-format workflow (FN-043/BRD-34). The API-format graph is a JSON object of
/// nodes keyed by id; each node has a <c>class_type</c> and an <c>inputs</c> object. Inputs whose
/// value is a JSON array (e.g. <c>["4", 0]</c>) are node-to-node links and are surfaced as
/// non-editable <c>link</c> inputs; scalar inputs (string/number/bool) are the editable,
/// parameterizable fields. Malformed JSON never throws to the caller — it returns a
/// <see cref="ComfyImportResult"/> with <see cref="ComfyImportResult.Ok"/> = false and a clear
/// error, so the endpoint answers 400 rather than 500.
/// </summary>
public class ComfyWorkflowParser
{
    /// <summary>
    /// Parses the workflow JSON into nodes and the flattened parameterizable-input list.
    /// </summary>
    /// <param name="aWorkflowJson">The ComfyUI API-format workflow JSON.</param>
    /// <returns>The parse result; <see cref="ComfyImportResult.Ok"/> is false on malformed input.</returns>
    public ComfyImportResult Parse(string aWorkflowJson)
    {
        if (string.IsNullOrWhiteSpace(aWorkflowJson))
        {
            return Fail("Workflow JSON is empty — paste or drag-drop a ComfyUI API-format workflow.");
        }

        JsonDocument vDocument;
        try
        {
            vDocument = JsonDocument.Parse(aWorkflowJson);
        }
        catch (JsonException vException)
        {
            return Fail($"Invalid JSON — {vException.Message}");
        }

        using (vDocument)
        {
            var vRoot = vDocument.RootElement;

            // ComfyUI's "Save (API Format)" export may either be the bare node map or wrap it under
            // a "prompt" property; accept both.
            if (vRoot.ValueKind == JsonValueKind.Object && vRoot.TryGetProperty("prompt", out var vPrompt)
                && vPrompt.ValueKind == JsonValueKind.Object)
            {
                vRoot = vPrompt;
            }

            if (vRoot.ValueKind != JsonValueKind.Object)
            {
                return Fail("Workflow JSON must be an object of nodes (ComfyUI API format), not an array or scalar.");
            }

            var vResult = new ComfyImportResult { Ok = true };
            foreach (var vNodeProperty in vRoot.EnumerateObject())
            {
                if (vNodeProperty.Value.ValueKind != JsonValueKind.Object
                    || !vNodeProperty.Value.TryGetProperty("class_type", out var vClassType)
                    || vClassType.ValueKind != JsonValueKind.String)
                {
                    // Not a node object (could be metadata); skip it rather than fail the whole parse.
                    continue;
                }

                var vNode = new ComfyNode
                {
                    NodeId = vNodeProperty.Name,
                    ClassType = vClassType.GetString() ?? string.Empty
                };

                if (vNodeProperty.Value.TryGetProperty("inputs", out var vInputs)
                    && vInputs.ValueKind == JsonValueKind.Object)
                {
                    foreach (var vInputProperty in vInputs.EnumerateObject())
                    {
                        var vInput = BuildInput(vInputProperty.Name, vInputProperty.Value);
                        vNode.Inputs.Add(vInput);

                        if (vInput.Kind == "scalar")
                        {
                            vResult.ParameterizableInputs.Add(new ComfyParameterizableInput
                            {
                                ParamKey = $"{vNode.NodeId}.{vInput.Name}",
                                NodeId = vNode.NodeId,
                                ClassType = vNode.ClassType,
                                InputName = vInput.Name,
                                InferredType = vInput.InferredType,
                                DefaultValue = vInput.Value
                            });
                        }
                    }
                }

                vResult.Nodes.Add(vNode);
            }

            if (vResult.Nodes.Count == 0)
            {
                return Fail("No ComfyUI nodes found — the JSON parsed but contained no nodes with a 'class_type'.");
            }

            vResult.NodeCount = vResult.Nodes.Count;
            return vResult;
        }
    }

    private static ComfyNodeInput BuildInput(string aName, JsonElement aValue)
    {
        // A node link is a JSON array of [sourceNodeId, outputIndex]; it is NOT editable.
        if (aValue.ValueKind == JsonValueKind.Array)
        {
            return new ComfyNodeInput { Name = aName, Kind = "link", Value = null, InferredType = "link" };
        }

        return aValue.ValueKind switch
        {
            JsonValueKind.String => new ComfyNodeInput { Name = aName, Kind = "scalar", Value = aValue.GetString(), InferredType = "text" },
            JsonValueKind.True or JsonValueKind.False => new ComfyNodeInput
            {
                Name = aName,
                Kind = "scalar",
                Value = aValue.GetBoolean() ? "true" : "false",
                InferredType = "bool"
            },
            JsonValueKind.Number => BuildNumberInput(aName, aValue),
            // Objects/null are not editable scalars — treat as an internal/link-like input.
            _ => new ComfyNodeInput { Name = aName, Kind = "link", Value = null, InferredType = "link" }
        };
    }

    private static ComfyNodeInput BuildNumberInput(string aName, JsonElement aValue)
    {
        var vRaw = aValue.GetRawText();
        // An integer literal (no '.', 'e' or 'E') maps to int; otherwise float.
        var vIsInteger = !vRaw.Contains('.') && !vRaw.Contains('e') && !vRaw.Contains('E');
        return new ComfyNodeInput
        {
            Name = aName,
            Kind = "scalar",
            Value = aValue.GetDecimal().ToString(CultureInfo.InvariantCulture),
            InferredType = vIsInteger ? "int" : "float"
        };
    }

    private static ComfyImportResult Fail(string aError) => new() { Ok = false, Error = aError };
}
