using System.Reflection;
using System.Text.Json;
using TrStudio.Models;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;

namespace TrStudio.Tests.Unit;

/// <summary>
/// Wave-0 smoke tests over the shared contract surface in TrStudio.Models: route constants
/// and JSON round-tripping of the registry contract.
/// </summary>
public class ContractsSmokeTests
{
    /// <summary>
    /// Every public route constant on ApiRoutes must be non-empty and rooted with a leading
    /// slash so clients and endpoint mappers can rely on absolute paths.
    /// </summary>
    [Fact]
    public void ApiRoutesAreRootedAndNonEmpty()
    {
        var vFields = typeof(ApiRoutes).GetFields(BindingFlags.Public | BindingFlags.Static);

        Assert.NotEmpty(vFields);
        Assert.All(vFields, aField =>
        {
            var vValue = Assert.IsType<string>(aField.GetValue(null));
            Assert.False(string.IsNullOrWhiteSpace(vValue));
            Assert.StartsWith("/", vValue);
        });
    }

    /// <summary>
    /// A fully-populated RegistryModel — including nested hardware, cost and declarative param
    /// specs — must survive a System.Text.Json serialize/deserialize round trip unchanged.
    /// </summary>
    [Fact]
    public void RegistryModelJsonRoundTrip()
    {
        var vModel = new RegistryModel
        {
            Id = "musetalk-v1",
            DisplayName = "MuseTalk (real-time lip-sync)",
            Task = GenerationTaskType.LipSync,
            ProviderKind = ProviderKind.ComfyUi,
            Tier = GenerationTier.Fast,
            WorkflowRef = "workflows/musetalk_v1.api.json",
            HardwareRequirements = new HardwareRequirements { MinVramGb = 8, Recommended = "A100", RunsOnMac = true },
            Params = new List<ModelParamSpec>
            {
                new() { Key = "sourceImage", Type = "image", Required = true },
                new() { Key = "resolution", Type = "enum", Values = new[] { "256", "512" }, DefaultValue = "256" }
            },
            Languages = new[] { "en", "hi", "language-agnostic" },
            License = "MIT",
            SafetyClass = SafetyClass.SyntheticDefault,
            Cost = new ModelCost { Kind = "serverless-seconds", EstimateSecPerJob = 90 },
            IsReady = true,
            IsEnabled = true
        };

        var vJson = JsonSerializer.Serialize(vModel);
        var vRoundTripped = JsonSerializer.Deserialize<RegistryModel>(vJson);

        Assert.NotNull(vRoundTripped);
        Assert.Equal(vModel.Id, vRoundTripped.Id);
        Assert.Equal(vModel.Task, vRoundTripped.Task);
        Assert.Equal(vModel.ProviderKind, vRoundTripped.ProviderKind);
        Assert.Equal(vModel.HardwareRequirements.MinVramGb, vRoundTripped.HardwareRequirements.MinVramGb);
        Assert.Equal(vModel.Params.Count, vRoundTripped.Params.Count);
        Assert.Equal(vModel.Params[1].Values, vRoundTripped.Params[1].Values);
        Assert.Equal(vModel.Cost.EstimateSecPerJob, vRoundTripped.Cost.EstimateSecPerJob);
    }

    /// <summary>
    /// The JobStatus provider record must use value equality so orchestrator code can compare
    /// consecutive status snapshots cheaply.
    /// </summary>
    [Fact]
    public void JobStatusRecordValueEquality()
    {
        var vFirst = new JobStatus(JobState.Running, 42, TimeSpan.FromSeconds(30), null, null);
        var vSecond = new JobStatus(JobState.Running, 42, TimeSpan.FromSeconds(30), null, null);

        Assert.Equal(vFirst, vSecond);
    }
}
