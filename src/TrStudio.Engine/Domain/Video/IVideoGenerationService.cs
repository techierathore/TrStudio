using TrStudio.Models.Contracts;

namespace TrStudio.Engine.Domain.Video;

/// <summary>
/// The video generation entry point (FN-051/052/BRD-42/43): accepts an I2V or T2V request with
/// duration/motion/aspect controls and first/last-frame + reference conditioning, tier-routes it to a
/// registry model + provider, and submits it through the proven job spine so the non-disableable
/// safety floors run and the spend cap is honoured. Every asset reference is owner-scoped (BRD-81).
/// </summary>
public interface IVideoGenerationService
{
    /// <summary>
    /// Validates and submits a video generation job.
    /// </summary>
    /// <param name="aRequest">The I2V/T2V request with controls + conditioning.</param>
    /// <param name="aUserId">The owning AppManager user id (owner scoping, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the submission.</param>
    /// <returns>
    /// The submission outcome: <c>queued</c> (with the resolved model/provider + degrade flag),
    /// <c>blocked</c> (a safety floor refused it) or <c>error</c> (validation / cost-cap failure).
    /// </returns>
    Task<VideoGenerateResponse> SubmitAsync(VideoGenerateRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);
}
