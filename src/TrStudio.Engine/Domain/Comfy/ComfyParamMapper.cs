using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;

namespace TrStudio.Engine.Domain.Comfy;

/// <summary>
/// Maps admin-chosen workflow inputs onto declarative registry param specs (FN-044/BRD-35). Only
/// inputs the parser exposed as editable scalars can be mapped: a mapping whose
/// <see cref="ComfyParamMapping.ParamKey"/> is a link/internal input (or does not exist) is
/// silently dropped, so internal/link nodes can never be exposed. The output is therefore exactly
/// the set of valid mappings.
/// </summary>
public class ComfyParamMapper
{
    private readonly ComfyWorkflowParser objParser;

    /// <summary>
    /// Initialises the mapper over the workflow parser.
    /// </summary>
    /// <param name="aParser">The parser that produces the parameterizable-input set.</param>
    public ComfyParamMapper(ComfyWorkflowParser aParser)
    {
        objParser = aParser;
    }

    /// <summary>
    /// Produces the registry param specs for the chosen mappings against a workflow.
    /// </summary>
    /// <param name="aWorkflowJson">The workflow JSON the mappings refer to.</param>
    /// <param name="aMappings">The admin-chosen input-to-param mappings.</param>
    /// <returns>Exactly the mapped params; malformed JSON or invalid mappings yield an empty/filtered list.</returns>
    public IReadOnlyList<ModelParamSpec> MapParams(string aWorkflowJson, IReadOnlyList<ComfyParamMapping> aMappings)
    {
        var vParsed = objParser.Parse(aWorkflowJson);
        if (!vParsed.Ok)
        {
            return Array.Empty<ModelParamSpec>();
        }

        var vInputs = vParsed.ParameterizableInputs.ToDictionary(aInput => aInput.ParamKey, StringComparer.Ordinal);
        var vSpecs = new List<ModelParamSpec>();

        foreach (var vMapping in aMappings)
        {
            // Drop any mapping that does not name an editable scalar input (hides internal/link nodes).
            if (!vInputs.TryGetValue(vMapping.ParamKey, out var vInput))
            {
                continue;
            }

            var vKey = string.IsNullOrWhiteSpace(vMapping.Key) ? vMapping.ParamKey : vMapping.Key!;
            vSpecs.Add(new ModelParamSpec
            {
                Key = vKey,
                Type = string.IsNullOrWhiteSpace(vMapping.Type) ? vInput.InferredType : vMapping.Type!,
                Required = vMapping.Required,
                DefaultValue = vMapping.DefaultValue ?? vInput.DefaultValue,
                Values = vMapping.Values,
                Min = vMapping.Min,
                Max = vMapping.Max,
                Label = vMapping.Label ?? vInput.InputName
            });
        }

        return vSpecs;
    }
}
