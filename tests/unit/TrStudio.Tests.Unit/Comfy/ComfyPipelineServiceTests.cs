using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Comfy;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Comfy;

/// <summary>
/// Unit tests for <see cref="ComfyPipelineService"/> (FN-048/049/050): save-as-model creates a
/// hidden-graph ComfyUi registry row with the mapped params, pin round-trips as the served
/// version, adding a version keeps the pin, and go-live is refused until the pinned version has a
/// passing sandbox smoke.
/// </summary>
public class ComfyPipelineServiceTests
{
    private const string SampleWorkflow =
        "{\"3\":{\"class_type\":\"KSampler\",\"inputs\":{\"seed\":1,\"steps\":20,\"model\":[\"4\",0]}}," +
        "\"6\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"text\":\"a cat\"}}}";

    private readonly Dictionary<string, RegistryModel> objModels = new();
    private readonly Dictionary<Guid, ComfyWorkflow> objWorkflows = new();
    private readonly Dictionary<Guid, ComfyWorkflowVersion> objVersions = new();

    private readonly IRegistryRepository objRegistry = Substitute.For<IRegistryRepository>();
    private readonly IComfyWorkflowRepository objWorkflowRepository = Substitute.For<IComfyWorkflowRepository>();

    private ComfyPipelineService CreateService(bool aSyntheticFallback = false)
    {
        var vParser = new ComfyWorkflowParser();
        objRegistry.UpsertAsync(Arg.Any<RegistryModel>(), Arg.Any<CancellationToken>())
            .Returns(aCall => { var vModel = aCall.Arg<RegistryModel>(); objModels[vModel.Id] = vModel; return Task.CompletedTask; });
        objRegistry.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(aCall => objModels.TryGetValue(aCall.Arg<string>(), out var vModel) ? vModel : null);

        objWorkflowRepository.InsertWorkflowAsync(Arg.Any<ComfyWorkflow>(), Arg.Any<CancellationToken>())
            .Returns(aCall => { var vWorkflow = aCall.Arg<ComfyWorkflow>(); objWorkflows[vWorkflow.WorkflowId] = vWorkflow; return Task.CompletedTask; });
        objWorkflowRepository.GetWorkflowAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(aCall => objWorkflows.TryGetValue(aCall.Arg<Guid>(), out var vWorkflow)
                && vWorkflow.OwnerUserId == aCall.ArgAt<string>(1) ? vWorkflow : null);
        objWorkflowRepository.InsertVersionAsync(Arg.Any<ComfyWorkflowVersion>(), Arg.Any<CancellationToken>())
            .Returns(aCall => { var vVersion = aCall.Arg<ComfyWorkflowVersion>(); objVersions[vVersion.VersionId] = vVersion; return Task.CompletedTask; });
        objWorkflowRepository.GetVersionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(aCall => objVersions.TryGetValue(aCall.Arg<Guid>(), out var vVersion) ? vVersion : null);
        objWorkflowRepository.NextVersionNumberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(aCall => objVersions.Values.Where(aVersion => aVersion.WorkflowId == aCall.Arg<Guid>())
                .Select(aVersion => aVersion.VersionNumber).DefaultIfEmpty(0).Max() + 1);
        objWorkflowRepository.UpdateSmokeStatusAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(aCall =>
            {
                if (objVersions.TryGetValue(aCall.Arg<Guid>(), out var vVersion))
                {
                    vVersion.SmokeStatus = aCall.ArgAt<string>(1);
                    vVersion.SmokeMessage = aCall.ArgAt<string?>(2);
                }

                return Task.CompletedTask;
            });

        var vOptions = new ComfyUiOptions { SandboxSyntheticFallback = aSyntheticFallback };
        return new ComfyPipelineService(
            vParser,
            new ComfyParamMapper(vParser),
            new ComfyDependencyScanner(vParser),
            new ComfyGraphBuilder(),
            objWorkflowRepository,
            objRegistry,
            vOptions,
            NullLogger<ComfyPipelineService>.Instance);
    }

    private static ComfySaveAsModelRequest SaveRequest() => new()
    {
        ModelId = "my-comfy-model",
        DisplayName = "My Comfy Model",
        Task = GenerationTaskType.WorkflowRun,
        Tier = GenerationTier.Fast,
        WorkflowJson = SampleWorkflow,
        IsEnabled = true,
        Mappings = new List<ComfyParamMapping>
        {
            new() { ParamKey = "6.text", Key = "prompt", Type = "text", Required = true },
            new() { ParamKey = "3.steps", Key = "steps" }
        }
    };

    /// <summary>Save-as-model creates a ComfyUi registry row pinned to v1, with the mapped params and IsReady false.</summary>
    [Fact]
    public async Task SaveAsModelCreatesHiddenGraphRegistryRow()
    {
        var vService = CreateService();

        var vModel = await vService.SaveAsModelAsync(SaveRequest(), "admin-1");

        Assert.Equal(ProviderKind.ComfyUi, vModel.ProviderKind);
        Assert.False(vModel.IsReady); // cannot be live without a passing smoke (FN-050)
        Assert.Equal(2, vModel.Params.Count);
        Assert.Contains(vModel.Params, aSpec => aSpec.Key == "prompt");
        // The pin points at a stored version; the graph lives only on the version row (hidden).
        var vServed = await vService.GetServedVersionAsync("my-comfy-model");
        Assert.NotNull(vServed);
        Assert.Equal(SampleWorkflow, objVersions[vServed!.Value].GraphJson);
    }

    /// <summary>Pinning a later version round-trips: the served version equals the pin.</summary>
    [Fact]
    public async Task PinRoundTripsAsServedVersion()
    {
        var vService = CreateService();
        await vService.SaveAsModelAsync(SaveRequest(), "admin-1");
        var vWorkflowId = objWorkflows.Keys.Single();

        var vVersion2 = await vService.AddVersionAsync(vWorkflowId, new ComfyAddVersionRequest { WorkflowJson = SampleWorkflow }, "admin-1");
        var vBeforePin = await vService.GetServedVersionAsync("my-comfy-model");

        // Adding v2 must NOT change what the model serves (it keeps serving its pinned v1).
        Assert.NotEqual(vVersion2.VersionId, vBeforePin);

        await vService.PinVersionAsync("my-comfy-model", vVersion2.VersionId);
        var vAfterPin = await vService.GetServedVersionAsync("my-comfy-model");
        Assert.Equal(vVersion2.VersionId, vAfterPin);
    }

    /// <summary>Go-live is refused until the pinned version has a passing sandbox smoke (the gate).</summary>
    [Fact]
    public async Task GoLiveRefusedWithoutPassingSmoke()
    {
        var vService = CreateService(aSyntheticFallback: false);
        await vService.SaveAsModelAsync(SaveRequest(), "admin-1");

        // No smoke yet → gate blocks.
        await Assert.ThrowsAsync<InvalidOperationException>(() => vService.GoLiveAsync("my-comfy-model"));

        // A degraded smoke (no ComfyUI, no fallback) still does not unlock go-live.
        var vSmoke = await vService.RunSmokeAsync("my-comfy-model");
        Assert.False(vSmoke.Passed);
        Assert.Equal("Degraded", vSmoke.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => vService.GoLiveAsync("my-comfy-model"));
        Assert.False(objModels["my-comfy-model"].IsReady);
    }

    /// <summary>After a passing sandbox smoke the model can go live (IsReady=true).</summary>
    [Fact]
    public async Task GoLiveAllowedAfterPassingSmoke()
    {
        var vService = CreateService(aSyntheticFallback: true);
        await vService.SaveAsModelAsync(SaveRequest(), "admin-1");

        var vSmoke = await vService.RunSmokeAsync("my-comfy-model");
        Assert.True(vSmoke.Passed);

        var vLive = await vService.GoLiveAsync("my-comfy-model");
        Assert.True(vLive.IsReady);
    }
}
