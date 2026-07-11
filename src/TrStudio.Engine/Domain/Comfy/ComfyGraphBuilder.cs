using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TrStudio.Engine.Domain.Comfy;

/// <summary>
/// Builds the exact API-format JSON submitted to ComfyUI's <c>/prompt</c> endpoint (FN-046/BRD-37).
/// Given the workflow graph and a set of parameter values keyed by <c>{NodeId}.{InputName}</c>,
/// it substitutes each value into the matching node input and wraps the graph in the
/// <c>{ "prompt": ... }</c> envelope ComfyUI expects. This is the precise payload the debug view
/// shows and the provider posts, so it is available with or without a live ComfyUI.
/// </summary>
public class ComfyGraphBuilder
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    /// <summary>
    /// Substitutes parameter values into the graph and returns the exact <c>/prompt</c> body JSON.
    /// </summary>
    /// <param name="aWorkflowJson">The workflow API-format graph JSON.</param>
    /// <param name="aParamsJson">Optional param values keyed by <c>{NodeId}.{InputName}</c>; ignored when null/blank.</param>
    /// <returns>The indented <c>{ "prompt": &lt;graph&gt; }</c> JSON, or the raw input wrapped verbatim when it cannot be parsed.</returns>
    public string BuildSubmittedJson(string aWorkflowJson, string? aParamsJson)
    {
        JsonNode? vGraph;
        try
        {
            vGraph = JsonNode.Parse(aWorkflowJson);
        }
        catch (JsonException)
        {
            // Not parseable: still surface exactly what would be sent, wrapped, for the debug view.
            return "{\"prompt\":" + (string.IsNullOrWhiteSpace(aWorkflowJson) ? "{}" : aWorkflowJson) + "}";
        }

        if (vGraph is JsonObject vGraphObject && vGraphObject["prompt"] is JsonObject vInner)
        {
            vGraph = vInner;
        }

        if (vGraph is JsonObject vNodes)
        {
            ApplyParameters(vNodes, aParamsJson);
        }

        var vEnvelope = new JsonObject { ["prompt"] = vGraph };
        return vEnvelope.ToJsonString(IndentedOptions);
    }

    private static void ApplyParameters(JsonObject aNodes, string? aParamsJson)
    {
        if (string.IsNullOrWhiteSpace(aParamsJson))
        {
            return;
        }

        JsonNode? vParams;
        try
        {
            vParams = JsonNode.Parse(aParamsJson);
        }
        catch (JsonException)
        {
            return;
        }

        if (vParams is not JsonObject vParamObject)
        {
            return;
        }

        foreach (var vParam in vParamObject)
        {
            var vSeparator = vParam.Key.IndexOf('.');
            if (vSeparator <= 0 || vSeparator >= vParam.Key.Length - 1)
            {
                continue;
            }

            var vNodeId = vParam.Key[..vSeparator];
            var vInputName = vParam.Key[(vSeparator + 1)..];

            if (aNodes[vNodeId] is JsonObject vNode && vNode["inputs"] is JsonObject vInputs)
            {
                vInputs[vInputName] = CoerceValue(vParam.Value);
            }
        }
    }

    private static JsonNode? CoerceValue(JsonNode? aValue)
    {
        // Preserve typed JSON values as-is; coerce a plain string that looks numeric/bool so the
        // submitted graph keeps ComfyUI's expected input types.
        if (aValue is not JsonValue vValue || !vValue.TryGetValue(out string? vText) || vText is null)
        {
            return aValue is null ? null : JsonNode.Parse(aValue.ToJsonString());
        }

        if (bool.TryParse(vText, out var vBool))
        {
            return JsonValue.Create(vBool);
        }

        if (long.TryParse(vText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var vLong))
        {
            return JsonValue.Create(vLong);
        }

        if (double.TryParse(vText, NumberStyles.Float, CultureInfo.InvariantCulture, out var vDouble))
        {
            return JsonValue.Create(vDouble);
        }

        return JsonValue.Create(vText);
    }
}
