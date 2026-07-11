using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Comfy;
using TrStudio.Engine.Providers;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Comfy;

/// <summary>
/// Unit tests for <see cref="ComfyUiProvider"/> (FN-045/046/BRD-36/37): provider routing gates to
/// ComfyUi models, and with no <c>ComfyUI:BaseUrl</c> configured a submitted job degrades cleanly
/// to Failed with an actionable "not configured" message and remediation hint — no crash, and the
/// HTTP client is never called.
/// </summary>
public class ComfyUiProviderTests
{
    private const string SampleWorkflow =
        "{\"3\":{\"class_type\":\"KSampler\",\"inputs\":{\"seed\":1}},\"9\":{\"class_type\":\"SaveImage\",\"inputs\":{\"images\":[\"8\",0]}}}";

    private readonly IRegistryRepository objRegistry = Substitute.For<IRegistryRepository>();
    private readonly IComfyWorkflowRepository objWorkflowRepository = Substitute.For<IComfyWorkflowRepository>();
    private readonly IHttpClientFactory objHttpClientFactory = Substitute.For<IHttpClientFactory>();

    private ComfyUiProvider CreateProvider(ComfyUiOptions aOptions)
    {
        var vParser = new ComfyWorkflowParser();
        return new ComfyUiProvider(
            objRegistry,
            objWorkflowRepository,
            new ComfyGraphBuilder(),
            new ComfyDependencyScanner(vParser),
            aOptions,
            objHttpClientFactory,
            NullLogger<ComfyUiProvider>.Instance);
    }

    private void SeedModel(string aModelId, Guid aVersionId)
    {
        objRegistry.GetAsync(aModelId, Arg.Any<CancellationToken>())
            .Returns(new RegistryModel { Id = aModelId, ProviderKind = ProviderKind.ComfyUi, WorkflowRef = aVersionId.ToString() });
        objWorkflowRepository.GetVersionAsync(aVersionId, Arg.Any<CancellationToken>())
            .Returns(new ComfyWorkflowVersion { VersionId = aVersionId, GraphJson = SampleWorkflow });
    }

    private static GenerationJob CreateJob(string aModelId) => new()
    {
        JobId = Guid.NewGuid(),
        UserId = "user-1",
        ModelId = aModelId,
        Task = GenerationTaskType.WorkflowRun,
        Tier = GenerationTier.Fast,
        ParamsJson = "{\"3.seed\":42}",
        CreatedUtc = DateTime.UtcNow
    };

    /// <summary>Supports returns true only for ComfyUi-kind registry rows.</summary>
    [Fact]
    public void SupportsOnlyComfyModels()
    {
        var vProvider = CreateProvider(new ComfyUiOptions());

        Assert.True(vProvider.Supports(GenerationTaskType.WorkflowRun, new RegistryModel { ProviderKind = ProviderKind.ComfyUi }));
        Assert.False(vProvider.Supports(GenerationTaskType.WorkflowRun, new RegistryModel { ProviderKind = ProviderKind.RunPod }));
    }

    /// <summary>
    /// With no base URL configured the job resolves to Failed carrying the actionable "not
    /// configured" message and a remediation hint — no crash, and no HTTP client is created.
    /// </summary>
    [Fact]
    public async Task NotConfiguredDegradesGracefully()
    {
        var vVersionId = Guid.NewGuid();
        SeedModel("comfy-1", vVersionId);
        var vProvider = CreateProvider(new ComfyUiOptions()); // empty BaseUrl

        var vJobId = await vProvider.SubmitAsync(CreateJob("comfy-1"), CancellationToken.None);
        var vStatus = await vProvider.GetStatusAsync(vJobId, CancellationToken.None);

        Assert.Equal(JobState.Failed, vStatus.State);
        Assert.Contains("not configured", vStatus.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(vStatus.RemediationHint));
        objHttpClientFactory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    /// <summary>A model with no pinned workflow version fails with an actionable pin hint.</summary>
    [Fact]
    public async Task NoPinnedVersionFailsActionably()
    {
        objRegistry.GetAsync("comfy-nopin", Arg.Any<CancellationToken>())
            .Returns(new RegistryModel { Id = "comfy-nopin", ProviderKind = ProviderKind.ComfyUi, WorkflowRef = null });
        var vProvider = CreateProvider(new ComfyUiOptions());

        var vJobId = await vProvider.SubmitAsync(CreateJob("comfy-nopin"), CancellationToken.None);
        var vStatus = await vProvider.GetStatusAsync(vJobId, CancellationToken.None);

        Assert.Equal(JobState.Failed, vStatus.State);
        Assert.Contains("pin", vStatus.RemediationHint, StringComparison.OrdinalIgnoreCase);
    }
}
