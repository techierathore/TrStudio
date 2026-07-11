using TrStudio.Models.Enums;

namespace TrStudio.Engine.Domain.Avatar;

/// <summary>
/// Default <see cref="IAvatarTierRouter"/> (FN-039/BRD-30). Maps each tier to the seeded lip-sync
/// registry model whose <c>ProviderKind</c> resolves to a different provider — so the same avatar job
/// spec routed at Fast / High / Premium runs on THREE different providers (local / RunPod / HeyGen)
/// with no other change. The model ids match the DbUp avatar seed (1110_DevSeedAvatar.sql).
/// </summary>
public class AvatarTierRouter : IAvatarTierRouter
{
    /// <summary>Fast tier → local lip-sync model (ComfyUi provider kind → AvatarProvider).</summary>
    public const string FastModelId = "musetalk-fast";

    /// <summary>High tier → RunPod serverless heavy model (RunPod provider kind → RunPodProvider).</summary>
    public const string HighModelId = "avatar-runpod-high";

    /// <summary>Premium tier → HeyGen API model (HeyGen provider kind → HeyGenProvider).</summary>
    public const string PremiumModelId = "heygen-premium";

    /// <inheritdoc />
    public string ResolveModelId(GenerationTier aTier) => aTier switch
    {
        GenerationTier.Fast => FastModelId,
        GenerationTier.High => HighModelId,
        GenerationTier.Premium => PremiumModelId,
        _ => FastModelId
    };
}
