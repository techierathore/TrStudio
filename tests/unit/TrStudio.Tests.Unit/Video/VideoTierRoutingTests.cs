using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Providers;
using TrStudio.Engine.Domain.RunPod;
using TrStudio.Engine.Domain.Video;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Engine.Providers;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Video;

/// <summary>
/// Unit tests for the video tier routing (FN-051/BRD-42): ONE tier control selects the registry
/// model per mode, and the registry resolver swaps the underlying provider for the SAME job spec —
/// High → RunPod (RunPodProvider), Premium → closed aggregator (ClosedApiProvider).
/// </summary>
public class VideoTierRoutingTests
{
    /// <summary>Each (mode, tier) pair maps to the seeded, distinct video model id.</summary>
    [Fact]
    public void EachModeTierMapsToADistinctModel()
    {
        var vRouter = new VideoTierRouter();

        Assert.Equal("video-t2v-fast", vRouter.ResolveModelId(GenerationTaskType.TextToVideo, GenerationTier.Fast));
        Assert.Equal("video-t2v-high", vRouter.ResolveModelId(GenerationTaskType.TextToVideo, GenerationTier.High));
        Assert.Equal("video-t2v-premium", vRouter.ResolveModelId(GenerationTaskType.TextToVideo, GenerationTier.Premium));
        Assert.Equal("video-i2v-fast", vRouter.ResolveModelId(GenerationTaskType.ImageToVideo, GenerationTier.Fast));
        Assert.Equal("video-i2v-high", vRouter.ResolveModelId(GenerationTaskType.ImageToVideo, GenerationTier.High));
        Assert.Equal("video-i2v-premium", vRouter.ResolveModelId(GenerationTaskType.ImageToVideo, GenerationTier.Premium));
    }

    /// <summary>
    /// FN-051 acceptance: the SAME video job spec routed at High vs Premium resolves to DIFFERENT
    /// concrete providers (RunPod vs the closed aggregator) with no other change.
    /// </summary>
    [Fact]
    public void HighAndPremiumResolveToDifferentProviders()
    {
        var vStorage = Substitute.For<IAssetStorage>();
        var vAssets = Substitute.For<IAssetRepository>();
        var vRunPodClient = Substitute.For<IRunPodClient>();

        var vResolver = new ProviderResolver(new IGenerationProvider[]
        {
            new RunPodProvider(vRunPodClient, new RunPodOptions(), vStorage, vAssets, NullLogger<RunPodProvider>.Instance),
            new ClosedApiProvider(new ClosedApiOptions(), vStorage, vAssets, NullLogger<ClosedApiProvider>.Instance)
        });

        var vHigh = vResolver.Resolve(VideoModel(ProviderKind.RunPod, GenerationTier.High));
        var vPremium = vResolver.Resolve(VideoModel(ProviderKind.ClosedApi, GenerationTier.Premium));

        Assert.IsType<RunPodProvider>(vHigh);
        Assert.IsType<ClosedApiProvider>(vPremium);
        Assert.NotEqual(vHigh.GetType(), vPremium.GetType());
    }

    /// <summary>The closed-API provider claims ONLY ClosedApi-kind models, keeping resolution unambiguous.</summary>
    [Fact]
    public void ClosedApiProviderGatesToClosedApiKindOnly()
    {
        var vProvider = new ClosedApiProvider(
            new ClosedApiOptions(),
            Substitute.For<IAssetStorage>(),
            Substitute.For<IAssetRepository>(),
            NullLogger<ClosedApiProvider>.Instance);

        Assert.True(vProvider.Supports(GenerationTaskType.TextToVideo, VideoModel(ProviderKind.ClosedApi, GenerationTier.Premium)));
        Assert.False(vProvider.Supports(GenerationTaskType.TextToVideo, VideoModel(ProviderKind.RunPod, GenerationTier.High)));
        Assert.False(vProvider.Supports(GenerationTaskType.ImageToVideo, VideoModel(ProviderKind.OnnxLocal, GenerationTier.Fast)));
    }

    private static RegistryModel VideoModel(ProviderKind aKind, GenerationTier aTier) => new()
    {
        Id = $"video-{aKind}",
        Task = GenerationTaskType.TextToVideo,
        ProviderKind = aKind,
        Tier = aTier,
        SafetyClass = SafetyClass.SyntheticDefault
    };
}
