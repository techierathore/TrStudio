using TrStudio.Engine.Domain.Comfy;

namespace TrStudio.Tests.Unit.Comfy;

/// <summary>
/// Unit tests for <see cref="ComfyWorkflowParser"/> (FN-043/BRD-34): a valid API-format workflow
/// yields nodes + parameterizable scalar inputs while link inputs are hidden, and malformed JSON
/// yields a clear parse error rather than throwing.
/// </summary>
public class ComfyWorkflowParserTests
{
    private const string SampleWorkflow =
        "{\"3\":{\"class_type\":\"KSampler\",\"inputs\":{\"seed\":1,\"steps\":20,\"cfg\":8.0,\"model\":[\"4\",0],\"positive\":[\"6\",0]}}," +
        "\"6\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"text\":\"a cat\"}}," +
        "\"9\":{\"class_type\":\"SaveImage\",\"inputs\":{\"images\":[\"8\",0]}}}";

    private static ComfyWorkflowParser CreateParser() => new();

    /// <summary>A valid workflow parses into its nodes with the correct count and class types.</summary>
    [Fact]
    public void ValidWorkflowParsesNodes()
    {
        var vResult = CreateParser().Parse(SampleWorkflow);

        Assert.True(vResult.Ok);
        Assert.Equal(3, vResult.NodeCount);
        Assert.Contains(vResult.Nodes, aNode => aNode.NodeId == "3" && aNode.ClassType == "KSampler");
        Assert.Contains(vResult.Nodes, aNode => aNode.ClassType == "SaveImage");
    }

    /// <summary>
    /// Only editable scalar inputs are parameterizable; node links (e.g. model/positive/images)
    /// are hidden. Types are inferred: seed → int, cfg → float, text → text.
    /// </summary>
    [Fact]
    public void ExposesOnlyScalarInputs()
    {
        var vResult = CreateParser().Parse(SampleWorkflow);

        var vKeys = vResult.ParameterizableInputs.Select(aInput => aInput.ParamKey).ToList();
        Assert.Contains("3.seed", vKeys);
        Assert.Contains("3.steps", vKeys);
        Assert.Contains("3.cfg", vKeys);
        Assert.Contains("6.text", vKeys);
        // Link inputs must never be offered for mapping.
        Assert.DoesNotContain("3.model", vKeys);
        Assert.DoesNotContain("3.positive", vKeys);
        Assert.DoesNotContain("9.images", vKeys);

        Assert.Equal("int", vResult.ParameterizableInputs.Single(aInput => aInput.ParamKey == "3.seed").InferredType);
        Assert.Equal("float", vResult.ParameterizableInputs.Single(aInput => aInput.ParamKey == "3.cfg").InferredType);
        Assert.Equal("text", vResult.ParameterizableInputs.Single(aInput => aInput.ParamKey == "6.text").InferredType);
    }

    /// <summary>Malformed JSON yields Ok=false with a clear error, never a throw.</summary>
    [Fact]
    public void MalformedJsonReturnsParseError()
    {
        var vResult = CreateParser().Parse("{not valid json");

        Assert.False(vResult.Ok);
        Assert.False(string.IsNullOrWhiteSpace(vResult.Error));
    }

    /// <summary>The "prompt"-wrapped export form is accepted and unwrapped.</summary>
    [Fact]
    public void AcceptsPromptWrappedExport()
    {
        var vResult = CreateParser().Parse("{\"prompt\":" + SampleWorkflow + "}");

        Assert.True(vResult.Ok);
        Assert.Equal(3, vResult.NodeCount);
    }
}
