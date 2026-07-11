using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.RunPod;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Engine.Providers;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.RunPod;

/// <summary>
/// Unit tests for <see cref="RunPodProvider"/> (REQ-FN-028/BRD-12): provider routing, the
/// graceful "not configured" degrade with an actionable remediation, and the configured happy
/// path driving submit → poll → success with scale-to-zero (idle-stop) against a faked client.
/// </summary>
public class RunPodProviderTests
{
    private readonly IRunPodClient objClient = Substitute.For<IRunPodClient>();
    private readonly IAssetStorage objAssetStorage = Substitute.For<IAssetStorage>();
    private readonly IAssetRepository objAssetRepository = Substitute.For<IAssetRepository>();

    private RunPodProvider CreateProvider(RunPodOptions aOptions)
    {
        objAssetStorage.SaveAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(aCall => Task.FromResult("/tmp/assets/" + aCall.Arg<string>()));
        return new RunPodProvider(objClient, aOptions, objAssetStorage, objAssetRepository, NullLogger<RunPodProvider>.Instance);
    }

    private static RunPodOptions Configured() => new() { ApiKey = "rp-key", EndpointId = "ep-1", AutoIdleStop = true };

    private static GenerationJob CreateJob() => new()
    {
        JobId = Guid.NewGuid(),
        UserId = "user-1",
        ModelId = "runpod-model",
        Task = GenerationTaskType.TextToImage,
        Tier = GenerationTier.High,
        ParamsJson = "{\"prompt\":\"a fox\"}",
        CreatedUtc = DateTime.UtcNow
    };

    private static async Task<TrStudio.Models.Providers.JobStatus> PollToTerminalAsync(RunPodProvider aProvider, Guid aJobId)
    {
        var vDeadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < vDeadline)
        {
            var vStatus = await aProvider.GetStatusAsync(aJobId, CancellationToken.None);
            if (vStatus.State is JobState.Succeeded or JobState.Failed)
            {
                return vStatus;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("RunPod job did not reach a terminal state in time.");
    }

    /// <summary>Supports returns true only for RunPod-kind registry rows.</summary>
    [Fact]
    public void SupportsOnlyRunPodModels()
    {
        var vProvider = CreateProvider(Configured());

        Assert.True(vProvider.Supports(GenerationTaskType.TextToImage, new RegistryModel { ProviderKind = ProviderKind.RunPod }));
        Assert.False(vProvider.Supports(GenerationTaskType.TextToImage, new RegistryModel { ProviderKind = ProviderKind.StubEcho }));
    }

    /// <summary>
    /// With no API key configured the job resolves to Failed carrying the actionable
    /// "RunPod not configured" message and a remediation hint — no crash, and the client is never
    /// called (graceful degrade).
    /// </summary>
    [Fact]
    public async Task NotConfiguredFailsGracefully()
    {
        var vProvider = CreateProvider(new RunPodOptions()); // empty ApiKey/EndpointId
        var vJob = CreateJob();

        var vJobId = await vProvider.SubmitAsync(vJob, CancellationToken.None);
        var vStatus = await vProvider.GetStatusAsync(vJobId, CancellationToken.None);

        Assert.Equal(JobState.Failed, vStatus.State);
        Assert.Contains("not configured", vStatus.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(vStatus.RemediationHint));
        await objClient.DidNotReceive().SubmitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A configured job submits to the client, polls to COMPLETED, persists the output asset and
    /// scales the endpoint to zero afterwards (auto idle-stop).
    /// </summary>
    [Fact]
    public async Task ConfiguredJobSucceedsAndScalesToZero()
    {
        objClient.SubmitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("run-1");
        objClient.GetRunStatusAsync("run-1", Arg.Any<CancellationToken>())
            .Returns(new RunPodRunStatus("COMPLETED", 100, "{}", null));

        var vProvider = CreateProvider(Configured());
        var vJob = CreateJob();

        var vJobId = await vProvider.SubmitAsync(vJob, CancellationToken.None);
        var vStatus = await PollToTerminalAsync(vProvider, vJobId);

        Assert.Equal(JobState.Succeeded, vStatus.State);
        var vResult = await vProvider.GetResultAsync(vJobId, CancellationToken.None);
        Assert.Equal("image", vResult.Kind);
        await objAssetRepository.Received(1).InsertAsync(
            Arg.Is<GeneratedAsset>(aAsset => aAsset.UserId == "user-1" && aAsset.JobId == vJobId),
            Arg.Any<CancellationToken>());
        await objClient.Received().SetMinWorkersAsync(0, Arg.Any<CancellationToken>());
    }

    /// <summary>A failed RunPod run surfaces Failed with the run error and a remediation hint.</summary>
    [Fact]
    public async Task FailedRunSurfacesError()
    {
        objClient.SubmitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("run-2");
        objClient.GetRunStatusAsync("run-2", Arg.Any<CancellationToken>())
            .Returns(new RunPodRunStatus("FAILED", 0, null, "worker crashed"));

        var vProvider = CreateProvider(Configured());

        var vJobId = await vProvider.SubmitAsync(CreateJob(), CancellationToken.None);
        var vStatus = await PollToTerminalAsync(vProvider, vJobId);

        Assert.Equal(JobState.Failed, vStatus.State);
        Assert.Contains("worker crashed", vStatus.FailureMessage);
        Assert.False(string.IsNullOrWhiteSpace(vStatus.RemediationHint));
    }
}
