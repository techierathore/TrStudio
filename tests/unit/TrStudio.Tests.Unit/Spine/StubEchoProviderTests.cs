using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Engine.Providers;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Spine;

/// <summary>
/// Unit tests for <see cref="StubEchoProvider"/> (REQ-FN-001): provider routing via Supports,
/// the simulated-progress success path persisting a placeholder PNG, and the deterministic
/// FAILME failure carrying an actionable remediation hint (REQ-FN-005).
/// </summary>
public class StubEchoProviderTests
{
    private readonly IAssetStorage objAssetStorage = Substitute.For<IAssetStorage>();
    private readonly IAssetRepository objAssetRepository = Substitute.For<IAssetRepository>();

    private StubEchoProvider CreateProvider()
    {
        objAssetStorage.SaveAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(aCall => Task.FromResult("/tmp/assets/" + aCall.Arg<string>()));
        return new StubEchoProvider(objAssetStorage, objAssetRepository, NullLogger<StubEchoProvider>.Instance);
    }

    private static GenerationJob CreateJob(string aParamsJson) => new()
    {
        JobId = Guid.NewGuid(),
        UserId = "user-1",
        ModelId = "stub-echo",
        Task = GenerationTaskType.TextToImage,
        Tier = GenerationTier.Fast,
        ParamsJson = aParamsJson,
        CreatedUtc = DateTime.UtcNow
    };

    private static async Task<TrStudio.Models.Providers.JobStatus> PollToTerminalAsync(StubEchoProvider aProvider, Guid aJobId)
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

        throw new TimeoutException("Stub job did not reach a terminal state in time.");
    }

    /// <summary>
    /// Supports returns true only for registry rows with the StubEcho provider kind, so the
    /// resolver routes each model to exactly one provider.
    /// </summary>
    [Fact]
    public void SupportsOnlyStubEchoModels()
    {
        var vProvider = CreateProvider();

        Assert.True(vProvider.Supports(GenerationTaskType.TextToImage, new RegistryModel { ProviderKind = ProviderKind.StubEcho }));
        Assert.False(vProvider.Supports(GenerationTaskType.TextToImage, new RegistryModel { ProviderKind = ProviderKind.ComfyUi }));
    }

    /// <summary>
    /// A submitted stub job progresses to Succeeded, persists a placeholder PNG binary plus a
    /// thumbnail through the asset store, inserts the owner-scoped asset row and exposes the
    /// asset reference via GetResult — the full provider contract end-to-end.
    /// </summary>
    [Fact]
    public async Task SubmitProducesPlaceholderAsset()
    {
        var vProvider = CreateProvider();
        var vJob = CreateJob("{\"prompt\":\"a red cube\",\"durationSeconds\":0.1}");

        var vJobId = await vProvider.SubmitAsync(vJob, CancellationToken.None);
        var vStatus = await PollToTerminalAsync(vProvider, vJobId);

        Assert.Equal(JobState.Succeeded, vStatus.State);
        Assert.Equal(100, vStatus.Progress);

        var vResult = await vProvider.GetResultAsync(vJobId, CancellationToken.None);
        Assert.Equal("image", vResult.Kind);
        await objAssetStorage.Received(2).SaveAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        await objAssetRepository.Received(1).InsertAsync(
            Arg.Is<GeneratedAsset>(aAsset => aAsset.UserId == "user-1" && aAsset.JobId == vJobId),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A prompt containing FAILME fails deterministically with a failure message and an
    /// actionable remediation hint, and GetResult refuses to return an asset.
    /// </summary>
    [Fact]
    public async Task FailMePromptFailsWithHint()
    {
        var vProvider = CreateProvider();
        var vJob = CreateJob("{\"prompt\":\"FAILME now\",\"durationSeconds\":0.1}");

        var vJobId = await vProvider.SubmitAsync(vJob, CancellationToken.None);
        var vStatus = await PollToTerminalAsync(vProvider, vJobId);

        Assert.Equal(JobState.Failed, vStatus.State);
        Assert.Contains("FAILME", vStatus.FailureMessage);
        Assert.False(string.IsNullOrWhiteSpace(vStatus.RemediationHint));
        await Assert.ThrowsAsync<InvalidOperationException>(() => vProvider.GetResultAsync(vJobId, CancellationToken.None));
    }

    /// <summary>
    /// Cost estimation for the stub provider is always zero (free local execution).
    /// </summary>
    [Fact]
    public async Task EstimateCostIsZero()
    {
        var vProvider = CreateProvider();

        var vCost = await vProvider.EstimateCostAsync(CreateJob("{}"), CancellationToken.None);

        Assert.Equal(0m, vCost);
    }
}
