using TrStudio.Engine.Domain.Comfy;
using TrStudio.Models.Contracts;

namespace TrStudio.Tests.Unit.Comfy;

/// <summary>
/// Unit tests for <see cref="ComfyParamMapper"/> (FN-044/BRD-35): a mapped workflow exposes exactly
/// the mapped params, and mappings that target internal/link inputs are dropped.
/// </summary>
public class ComfyParamMapperTests
{
    private const string SampleWorkflow =
        "{\"3\":{\"class_type\":\"KSampler\",\"inputs\":{\"seed\":1,\"steps\":20,\"model\":[\"4\",0]}}," +
        "\"6\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"text\":\"a cat\"}}}";

    private static ComfyParamMapper CreateMapper() => new(new ComfyWorkflowParser());

    /// <summary>Exactly the two mapped scalar inputs are exposed, with overridden keys/required/defaults.</summary>
    [Fact]
    public void ExposesExactlyMappedParams()
    {
        var vMappings = new List<ComfyParamMapping>
        {
            new() { ParamKey = "6.text", Key = "prompt", Type = "text", Required = true, Label = "Prompt" },
            new() { ParamKey = "3.steps", Key = "steps", Required = false, DefaultValue = "25" }
        };

        var vSpecs = CreateMapper().MapParams(SampleWorkflow, vMappings);

        Assert.Equal(2, vSpecs.Count);
        var vPrompt = vSpecs.Single(aSpec => aSpec.Key == "prompt");
        Assert.True(vPrompt.Required);
        Assert.Equal("Prompt", vPrompt.Label);
        var vSteps = vSpecs.Single(aSpec => aSpec.Key == "steps");
        Assert.Equal("int", vSteps.Type); // inferred when the mapping leaves Type null
        Assert.Equal("25", vSteps.DefaultValue);
    }

    /// <summary>A mapping targeting a link input (3.model) is dropped — internal/link nodes stay hidden.</summary>
    [Fact]
    public void DropsLinkInputMappings()
    {
        var vMappings = new List<ComfyParamMapping>
        {
            new() { ParamKey = "6.text", Key = "prompt" },
            new() { ParamKey = "3.model", Key = "model" },   // link — must be dropped
            new() { ParamKey = "99.nope", Key = "ghost" }    // non-existent — must be dropped
        };

        var vSpecs = CreateMapper().MapParams(SampleWorkflow, vMappings);

        Assert.Single(vSpecs);
        Assert.Equal("prompt", vSpecs[0].Key);
    }
}
