using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Avatar;
using TrStudio.Engine.Domain.Comfy;
using TrStudio.Engine.Domain.Providers;
using TrStudio.Engine.Domain.RunPod;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Engine.Providers;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Avatar;

/// <summary>
/// Unit tests for the avatar tier routing (FN-039/BRD-30): ONE tier control selects the registry
/// model, and the registry resolver swaps the underlying provider for the SAME job spec — Fast →
/// local (AvatarProvider / ComfyUi), High → RunPod (RunPodProvider), Premium → HeyGen (HeyGenProvider).
/// </summary>
public class AvatarTierRoutingTests
{
    /// <summary>Each tier maps to a distinct lip-sync registry model id.</summary>
    [Fact]
    public void EachTierMapsToADistinctModel()
    {
        var vRouter = new AvatarTierRouter();

        var vFast = vRouter.ResolveModelId(GenerationTier.Fast);
        var vHigh = vRouter.ResolveModelId(GenerationTier.High);
        var vPremium = vRouter.ResolveModelId(GenerationTier.Premium);

        Assert.Equal("musetalk-fast", vFast);
        Assert.Equal("avatar-runpod-high", vHigh);
        Assert.Equal("heygen-premium", vPremium);
        Assert.Equal(3, new[] { vFast, vHigh, vPremium }.Distinct().Count());
    }

    /// <summary>
    /// FN-039 acceptance: the SAME avatar job spec routed at Fast / High / Premium resolves to THREE
    /// DIFFERENT concrete providers with no other change.
    /// </summary>
    [Fact]
    public void SameSpecResolvesToThreeDifferentProviders()
    {
        var vStorage = Substitute.For<IAssetStorage>();
        var vAssets = Substitute.For<IAssetRepository>();
        var vRunPodClient = Substitute.For<IRunPodClient>();

        var vResolver = new ProviderResolver(new IGenerationProvider[]
        {
            new AvatarProvider(new AvatarOptions(), vStorage, vAssets, NullLogger<AvatarProvider>.Instance),
            new RunPodProvider(vRunPodClient, new RunPodOptions(), vStorage, vAssets, NullLogger<RunPodProvider>.Instance),
            new HeyGenProvider(new AvatarOptions(), vStorage, vAssets, NullLogger<HeyGenProvider>.Instance)
        });

        var vFast = vResolver.Resolve(LipSyncModel(ProviderKind.ComfyUi, GenerationTier.Fast));
        var vHigh = vResolver.Resolve(LipSyncModel(ProviderKind.RunPod, GenerationTier.High));
        var vPremium = vResolver.Resolve(LipSyncModel(ProviderKind.HeyGen, GenerationTier.Premium));

        Assert.IsType<AvatarProvider>(vFast);
        Assert.IsType<RunPodProvider>(vHigh);
        Assert.IsType<HeyGenProvider>(vPremium);
        Assert.Equal(3, new[] { vFast.GetType(), vHigh.GetType(), vPremium.GetType() }.Distinct().Count());
    }

    /// <summary>
    /// Regression (UAT 2026-07-04): AvatarProvider and ComfyUiProvider BOTH back <see cref="ProviderKind.ComfyUi"/>
    /// models, so with both registered the resolver must still resolve to exactly one via the task
    /// discriminator — a ComfyUi+LipSync row (musetalk-fast) → AvatarProvider, a ComfyUi+WorkflowRun row
    /// (a saved workflow / comfy image model) → ComfyUiProvider. Before the fix both claimed every
    /// ComfyUi row and the resolver threw "Multiple generation providers claim model …" (HTTP 500 on
    /// the avatar Fast tier and on any ComfyUi-kind job).
    /// </summary>
    [Fact]
    public void AvatarAndComfyProvidersDoNotCollideOnComfyModels()
    {
        var vStorage = Substitute.For<IAssetStorage>();
        var vAssets = Substitute.For<IAssetRepository>();

        var vResolver = new ProviderResolver(new IGenerationProvider[]
        {
            new AvatarProvider(new AvatarOptions(), vStorage, vAssets, NullLogger<AvatarProvider>.Instance),
            CreateComfyProvider()
        });

        var vLipSync = vResolver.Resolve(ComfyModel("musetalk-fast", GenerationTaskType.LipSync));
        var vWorkflow = vResolver.Resolve(ComfyModel("p6-comfy-portrait", GenerationTaskType.WorkflowRun));

        Assert.IsType<AvatarProvider>(vLipSync);
        Assert.IsType<ComfyUiProvider>(vWorkflow);
    }

    private static ComfyUiProvider CreateComfyProvider()
    {
        var vParser = new ComfyWorkflowParser();
        return new ComfyUiProvider(
            Substitute.For<IRegistryRepository>(),
            Substitute.For<IComfyWorkflowRepository>(),
            new ComfyGraphBuilder(),
            new ComfyDependencyScanner(vParser),
            new ComfyUiOptions(),
            Substitute.For<IHttpClientFactory>(),
            NullLogger<ComfyUiProvider>.Instance);
    }

    private static RegistryModel ComfyModel(string aId, GenerationTaskType aTask) => new()
    {
        Id = aId,
        Task = aTask,
        ProviderKind = ProviderKind.ComfyUi,
        Tier = GenerationTier.Fast,
        SafetyClass = SafetyClass.SyntheticDefault
    };

    private static RegistryModel LipSyncModel(ProviderKind aKind, GenerationTier aTier) => new()
    {
        Id = $"lipsync-{aKind}",
        Task = GenerationTaskType.LipSync,
        ProviderKind = aKind,
        Tier = aTier,
        SafetyClass = SafetyClass.SyntheticDefault
    };
}
