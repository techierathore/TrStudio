using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Avatar;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Engine.Providers;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Avatar;

/// <summary>
/// Unit tests for <see cref="AvatarProvider"/> and <see cref="HeyGenProvider"/> (FN-037/BRD-28):
/// provider routing by kind, the graceful "model/key not configured" degrade with an actionable
/// remediation, and the configured/synthetic happy path persisting a video asset.
/// </summary>
public class AvatarProviderTests
{
    private readonly IAssetStorage objStorage = Substitute.For<IAssetStorage>();
    private readonly IAssetRepository objAssets = Substitute.For<IAssetRepository>();

    public AvatarProviderTests()
    {
        objStorage.SaveAsync(Arg.Any<string>(), Arg.Any<byte[]>())
            .Returns(aCall => Task.FromResult("/tmp/assets/" + aCall.Arg<string>()));
    }

    private static GenerationJob LipSyncJob() => new()
    {
        JobId = Guid.NewGuid(),
        UserId = "user-1",
        ModelId = "musetalk-fast",
        Task = GenerationTaskType.LipSync,
        Tier = GenerationTier.Fast,
        ParamsJson = "{\"characterId\":\"c1\"}",
        CreatedUtc = DateTime.UtcNow
    };

    private static async Task<JobStatus> PollAsync(IGenerationProvider aProvider, Guid aJobId)
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

        throw new TimeoutException("Avatar job did not reach a terminal state in time.");
    }

    /// <summary>AvatarProvider claims only ComfyUi-kind rows; HeyGenProvider only HeyGen-kind rows.</summary>
    [Fact]
    public void ProvidersClaimOnlyTheirKind()
    {
        var vAvatar = new AvatarProvider(new AvatarOptions(), objStorage, objAssets, NullLogger<AvatarProvider>.Instance);
        var vHeyGen = new HeyGenProvider(new AvatarOptions(), objStorage, objAssets, NullLogger<HeyGenProvider>.Instance);

        Assert.True(vAvatar.Supports(GenerationTaskType.LipSync, new RegistryModel { ProviderKind = ProviderKind.ComfyUi }));
        Assert.False(vAvatar.Supports(GenerationTaskType.LipSync, new RegistryModel { ProviderKind = ProviderKind.HeyGen }));
        Assert.True(vHeyGen.Supports(GenerationTaskType.LipSync, new RegistryModel { ProviderKind = ProviderKind.HeyGen }));
        Assert.False(vHeyGen.Supports(GenerationTaskType.LipSync, new RegistryModel { ProviderKind = ProviderKind.ComfyUi }));
    }

    /// <summary>With no local lip-sync model the Fast provider degrades gracefully (actionable message, no crash).</summary>
    [Fact]
    public async Task LocalNotInstalledDegradesGracefully()
    {
        var vProvider = new AvatarProvider(new AvatarOptions(), objStorage, objAssets, NullLogger<AvatarProvider>.Instance);

        var vJobId = await vProvider.SubmitAsync(LipSyncJob(), CancellationToken.None);
        var vStatus = await vProvider.GetStatusAsync(vJobId, CancellationToken.None);

        Assert.Equal(JobState.Failed, vStatus.State);
        Assert.Contains("not installed", vStatus.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(vStatus.RemediationHint));
        await objAssets.DidNotReceive().InsertAsync(Arg.Any<GeneratedAsset>());
    }

    /// <summary>With the dev synthetic fallback on, the Fast provider persists a placeholder video asset.</summary>
    [Fact]
    public async Task SyntheticFallbackPersistsVideoAsset()
    {
        var vProvider = new AvatarProvider(
            new AvatarOptions { SyntheticFallback = true }, objStorage, objAssets, NullLogger<AvatarProvider>.Instance);

        var vJob = LipSyncJob();
        var vJobId = await vProvider.SubmitAsync(vJob, CancellationToken.None);
        var vStatus = await PollAsync(vProvider, vJobId);

        Assert.Equal(JobState.Succeeded, vStatus.State);
        var vResult = await vProvider.GetResultAsync(vJobId, CancellationToken.None);
        Assert.Equal("video", vResult.Kind);
        await objAssets.Received(1).InsertAsync(Arg.Is<GeneratedAsset>(aAsset => aAsset.Kind == "video" && aAsset.UserId == "user-1"));
    }

    /// <summary>With no HeyGen API key the Premium provider degrades gracefully (actionable message, no crash).</summary>
    [Fact]
    public async Task HeyGenNotConfiguredDegradesGracefully()
    {
        var vProvider = new HeyGenProvider(new AvatarOptions(), objStorage, objAssets, NullLogger<HeyGenProvider>.Instance);

        var vJobId = await vProvider.SubmitAsync(LipSyncJob(), CancellationToken.None);
        var vStatus = await vProvider.GetStatusAsync(vJobId, CancellationToken.None);

        Assert.Equal(JobState.Failed, vStatus.State);
        Assert.Contains("HeyGen not configured", vStatus.FailureMessage);
        Assert.False(string.IsNullOrWhiteSpace(vStatus.RemediationHint));
        await objAssets.DidNotReceive().InsertAsync(Arg.Any<GeneratedAsset>());
    }

    /// <summary>With a HeyGen key configured the Premium provider persists a placeholder video asset.</summary>
    [Fact]
    public async Task HeyGenConfiguredPersistsVideoAsset()
    {
        var vProvider = new HeyGenProvider(
            new AvatarOptions { HeyGenApiKey = "hg-key" }, objStorage, objAssets, NullLogger<HeyGenProvider>.Instance);

        var vJobId = await vProvider.SubmitAsync(LipSyncJob(), CancellationToken.None);
        var vStatus = await PollAsync(vProvider, vJobId);

        Assert.Equal(JobState.Succeeded, vStatus.State);
        var vResult = await vProvider.GetResultAsync(vJobId, CancellationToken.None);
        Assert.Equal("video", vResult.Kind);
    }
}
