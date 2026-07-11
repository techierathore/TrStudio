using TrStudio.Engine.Domain.Comfy;

namespace TrStudio.Tests.Unit.Comfy;

/// <summary>
/// Unit tests for <see cref="ComfyDependencyScanner"/> (FN-047/BRD-38): a workflow with a
/// non-built-in node reports it as a missing custom node with an install hint, and loader model
/// files are reported as missing.
/// </summary>
public class ComfyDependencyScannerTests
{
    private static ComfyDependencyScanner CreateScanner() => new(new ComfyWorkflowParser());

    /// <summary>A custom (non-built-in) node class is reported as a missing custom node with a hint.</summary>
    [Fact]
    public void ReportsMissingCustomNode()
    {
        const string vWorkflow =
            "{\"1\":{\"class_type\":\"MyFancySamplerXYZ\",\"inputs\":{\"steps\":10}}," +
            "\"2\":{\"class_type\":\"SaveImage\",\"inputs\":{\"images\":[\"1\",0]}}}";

        var vReport = CreateScanner().Scan(vWorkflow);

        Assert.Contains("MyFancySamplerXYZ", vReport.RequiredCustomNodes);
        Assert.Contains(vReport.MissingCustomNodes, aDependency => aDependency.Name == "MyFancySamplerXYZ");
        Assert.True(vReport.HasMissing);
        Assert.False(string.IsNullOrWhiteSpace(vReport.MissingCustomNodes[0].InstallHint));
        // SaveImage is built in and must NOT be reported.
        Assert.DoesNotContain("SaveImage", vReport.RequiredCustomNodes);
    }

    /// <summary>A checkpoint loader's ckpt_name is reported as a required + missing model file.</summary>
    [Fact]
    public void ReportsMissingModelFile()
    {
        const string vWorkflow =
            "{\"4\":{\"class_type\":\"CheckpointLoaderSimple\",\"inputs\":{\"ckpt_name\":\"dreamshaper_8.safetensors\"}}}";

        var vReport = CreateScanner().Scan(vWorkflow);

        Assert.Contains("dreamshaper_8.safetensors", vReport.RequiredModelFiles);
        Assert.Contains(vReport.MissingModelFiles, aDependency => aDependency.Name == "dreamshaper_8.safetensors" && aDependency.Kind == "model-file");
    }

    /// <summary>An all-built-in workflow with no model files reports nothing missing.</summary>
    [Fact]
    public void BuiltinOnlyReportsNothing()
    {
        const string vWorkflow =
            "{\"6\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"text\":\"a cat\"}}," +
            "\"9\":{\"class_type\":\"SaveImage\",\"inputs\":{\"images\":[\"8\",0]}}}";

        var vReport = CreateScanner().Scan(vWorkflow);

        Assert.False(vReport.HasMissing);
    }
}
