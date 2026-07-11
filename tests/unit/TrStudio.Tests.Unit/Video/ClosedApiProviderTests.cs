using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Video;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Engine.Providers;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Video;

/// <summary>
/// Unit tests for the closed-API aggregator provider (FN-051/BRD-42): with no ClosedApi:ApiKey it
/// degrades gracefully to a Failed status carrying an actionable remediation instead of crashing or
/// producing an asset.
/// </summary>
public class ClosedApiProviderTests
{
    /// <summary>Without an aggregator key the job fails cleanly with the "not configured" remediation.</summary>
    [Fact]
    public async Task DegradesGracefullyWithoutApiKey()
    {
        var vAssets = Substitute.For<IAssetRepository>();
        var vProvider = new ClosedApiProvider(
            new ClosedApiOptions(),
            Substitute.For<IAssetStorage>(),
            vAssets,
            NullLogger<ClosedApiProvider>.Instance);

        var vJob = new GenerationJob
        {
            JobId = Guid.NewGuid(),
            UserId = "user-1",
            ModelId = "video-t2v-premium",
            Task = GenerationTaskType.TextToVideo
        };

        await vProvider.SubmitAsync(vJob, CancellationToken.None);
        var vStatus = await vProvider.GetStatusAsync(vJob.JobId, CancellationToken.None);

        Assert.Equal(JobState.Failed, vStatus.State);
        Assert.Contains("aggregator not configured", vStatus.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(vStatus.RemediationHint));
        await vAssets.DidNotReceiveWithAnyArgs().InsertAsync(default!, default);
    }

    /// <summary>A configured aggregator key emits a placeholder video asset (the plumbing proof).</summary>
    [Fact]
    public async Task ConfiguredKeyProducesVideoAsset()
    {
        var vAssets = Substitute.For<IAssetRepository>();
        var vStorage = Substitute.For<IAssetStorage>();
        vStorage.SaveAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(aCall => Task.FromResult("/assets/" + aCall.ArgAt<string>(0)));

        var vProvider = new ClosedApiProvider(
            new ClosedApiOptions { ApiKey = "test-key", Vendor = "fal" },
            vStorage,
            vAssets,
            NullLogger<ClosedApiProvider>.Instance);

        var vJob = new GenerationJob
        {
            JobId = Guid.NewGuid(),
            UserId = "user-1",
            ModelId = "video-t2v-premium",
            Task = GenerationTaskType.TextToVideo
        };

        await vProvider.SubmitAsync(vJob, CancellationToken.None);

        // Poll to completion (the configured path runs a short simulated progress loop).
        JobStatus vStatus;
        var vDeadline = DateTime.UtcNow.AddSeconds(10);
        do
        {
            await Task.Delay(50);
            vStatus = await vProvider.GetStatusAsync(vJob.JobId, CancellationToken.None);
        }
        while (vStatus.State is JobState.Running or JobState.Pending && DateTime.UtcNow < vDeadline);

        Assert.Equal(JobState.Succeeded, vStatus.State);
        await vAssets.Received(1).InsertAsync(Arg.Is<GeneratedAsset>(aAsset => aAsset.Kind == "video"), Arg.Any<CancellationToken>());
    }
}
