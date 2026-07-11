using TrStudio.Models.Enums;

namespace TrStudio.Engine.Domain.Video;

/// <summary>
/// Routes a video generation task + tier to the seeded registry model whose <c>ProviderKind</c>
/// resolves to a distinct provider (FN-051/BRD-42), so the SAME video job spec runs on a different
/// provider per tier with no other change: Fast → local, High → RunPod, Premium → closed aggregator.
/// </summary>
public interface IVideoTierRouter
{
    /// <summary>
    /// Resolves the registry model id backing a (task, tier) pair.
    /// </summary>
    /// <param name="aTask">The video task (<see cref="GenerationTaskType.TextToVideo"/> or <see cref="GenerationTaskType.ImageToVideo"/>).</param>
    /// <param name="aTier">The execution tier.</param>
    /// <returns>The seeded registry model id.</returns>
    string ResolveModelId(GenerationTaskType aTask, GenerationTier aTier);
}
