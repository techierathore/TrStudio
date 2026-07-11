using TrStudio.Models.Contracts;

namespace TrStudio.Engine.Domain.Video;

/// <summary>
/// The video post-processing steps (FN-053/BRD-44): frame interpolation (RIFE/FILM) and video
/// upscale, applied to an owner-scoped source clip. Both are structural post-steps that degrade
/// cleanly when their model is not installed. Returns <c>null</c> when the source asset is not found
/// or not owned by the caller (BRD-81).
/// </summary>
public interface IVideoPostProcessor
{
    /// <summary>
    /// Frame-interpolates an owner-scoped source clip to a higher effective frame rate.
    /// </summary>
    /// <param name="aRequest">The post-step request (source asset + interpolation factor).</param>
    /// <param name="aUserId">The owning AppManager user id (owner scoping, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The step outcome, or null when the source asset is not found/owned.</returns>
    Task<VideoPostStepResponse?> InterpolateAsync(VideoPostStepRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Spatially upscales an owner-scoped source clip.
    /// </summary>
    /// <param name="aRequest">The post-step request (source asset + scale factor).</param>
    /// <param name="aUserId">The owning AppManager user id (owner scoping, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The step outcome, or null when the source asset is not found/owned.</returns>
    Task<VideoPostStepResponse?> UpscaleAsync(VideoPostStepRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);
}
