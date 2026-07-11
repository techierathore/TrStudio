using TrStudio.Models.Enums;

namespace TrStudio.Engine.Domain.Video;

/// <summary>
/// Default <see cref="IVideoTierRouter"/> (FN-051/BRD-42). Maps each (task, tier) pair to the seeded
/// video registry model whose <c>ProviderKind</c> resolves to a different provider — so the same
/// video job spec routed at Fast / High / Premium runs on THREE different providers with no other
/// change: Fast → OnnxLocal (local), High → RunPod (serverless GPU), Premium → ClosedApi (aggregator).
/// The model ids match the DbUp video seed (1300_DevSeedVideo.sql).
/// </summary>
public class VideoTierRouter : IVideoTierRouter
{
    /// <inheritdoc />
    public string ResolveModelId(GenerationTaskType aTask, GenerationTier aTier)
    {
        var vMode = aTask == GenerationTaskType.ImageToVideo ? "i2v" : "t2v";
        var vTier = aTier switch
        {
            GenerationTier.High => "high",
            GenerationTier.Premium => "premium",
            _ => "fast"
        };

        return $"video-{vMode}-{vTier}";
    }
}
