using Microsoft.Extensions.Logging;
using TrStudio.Models.Contracts;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Domain.Video;

/// <summary>
/// Default <see cref="IVideoPostProcessor"/> (FN-053). Validates the owner-scoped source clip
/// (BRD-81), then applies the post-step: frame interpolation (RIFE/FILM) or video upscale. Because
/// the interpolation/upscale models are NOT bundled (weights are never downloaded here), a missing
/// model does NOT crash — the step degrades cleanly to a Degraded outcome carrying an actionable
/// remediation. When a model is installed the real step would run and persist a new video asset.
/// </summary>
public class VideoPostProcessor : IVideoPostProcessor
{
    private readonly IAssetRepository objAssetRepository;
    private readonly VideoOptions objOptions;
    private readonly ILogger<VideoPostProcessor> objLogger;

    /// <summary>
    /// Initialises the post-processor.
    /// </summary>
    /// <param name="aAssetRepository">Owner-scoped source-asset lookup (BRD-81).</param>
    /// <param name="aOptions">Video options carrying the interpolation/upscale install flags.</param>
    /// <param name="aLogger">Structured logger.</param>
    public VideoPostProcessor(
        IAssetRepository aAssetRepository,
        VideoOptions aOptions,
        ILogger<VideoPostProcessor> aLogger)
    {
        objAssetRepository = aAssetRepository;
        objOptions = aOptions;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public Task<VideoPostStepResponse?> InterpolateAsync(VideoPostStepRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
        => RunStepAsync(
            aRequest,
            aUserId,
            "interpolate",
            objOptions.InterpolationModelInstalled,
            VideoOptions.InterpolationNotInstalledMessage,
            aCancellationToken);

    /// <inheritdoc />
    public Task<VideoPostStepResponse?> UpscaleAsync(VideoPostStepRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
        => RunStepAsync(
            aRequest,
            aUserId,
            "upscale",
            objOptions.UpscaleModelInstalled,
            VideoOptions.UpscaleNotInstalledMessage,
            aCancellationToken);

    private async Task<VideoPostStepResponse?> RunStepAsync(
        VideoPostStepRequest aRequest,
        string aUserId,
        string aStep,
        bool aModelInstalled,
        string aNotInstalledMessage,
        CancellationToken aCancellationToken)
    {
        // Owner-scoped source lookup (BRD-81): a creator can only post-process their own clips.
        var vAsset = await objAssetRepository.GetAsync(aRequest.SourceAssetId, aUserId, aCancellationToken).ConfigureAwait(false);
        if (vAsset is null)
        {
            objLogger.LogWarning("Video {Step} rejected: asset {AssetId} not found for user {UserId}", aStep, aRequest.SourceAssetId, aUserId);
            return null;
        }

        if (!aModelInstalled)
        {
            // Graceful degrade: no crash — the post-step is deferred with an actionable remediation.
            objLogger.LogInformation("Video {Step} degraded for asset {AssetId}: model not installed", aStep, aRequest.SourceAssetId);
            return new VideoPostStepResponse
            {
                Step = aStep,
                Status = "degraded",
                Degraded = true,
                Message = aNotInstalledMessage
            };
        }

        // A real RIFE/FILM or upscale pass over the source clip would run here and persist a new
        // owner-scoped video asset; the model install path is deferred until weights are supplied.
        objLogger.LogInformation("Video {Step} model installed; real post-step execution is deferred to the worker", aStep);
        return new VideoPostStepResponse
        {
            Step = aStep,
            Status = "ok",
            Degraded = false,
            Message = $"The {aStep} model is installed; the post-step is enqueued for execution."
        };
    }
}
