using Microsoft.Extensions.Configuration;

namespace TrStudio.Engine.Domain.Video;

/// <summary>
/// Configuration for the video vertical (section <c>Video</c>, FN-051..054). No local video model,
/// interpolation model or upscale model is installed by default, so the Fast generation tier and the
/// FN-053 post-steps degrade gracefully with an actionable message (REQ-FN-005 style). The
/// installed-flags are the single source of truth the pipeline reads to decide whether a step runs
/// for real or degrades cleanly.
/// </summary>
public class VideoOptions
{
    /// <summary>Whether a local text/image-to-video model is installed (backs the Fast tier).</summary>
    public bool LocalVideoModelInstalled { get; set; }

    /// <summary>Whether a frame-interpolation model (RIFE / FILM) is installed for the interpolate post-step.</summary>
    public bool InterpolationModelInstalled { get; set; }

    /// <summary>Whether a video upscale model is installed for the upscale post-step.</summary>
    public bool UpscaleModelInstalled { get; set; }

    /// <summary>The actionable message surfaced when no local video model is installed (Fast tier).</summary>
    public const string LocalNotInstalledMessage =
        "Local video model not installed — install a local T2V/I2V model (e.g. AnimateDiff / CogVideoX / LTX) " +
        "under Admin > Models for the Fast tier, or route this job to the High (RunPod) or Premium (closed API) tier.";

    /// <summary>The actionable message surfaced when the frame-interpolation model is not installed.</summary>
    public const string InterpolationNotInstalledMessage =
        "Frame interpolation model not installed — install a RIFE or FILM model under Admin > Models to enable " +
        "the interpolate post-step.";

    /// <summary>The actionable message surfaced when the video upscale model is not installed.</summary>
    public const string UpscaleNotInstalledMessage =
        "Video upscale model not installed — install a video upscale model under Admin > Models to enable the " +
        "upscale post-step.";

    /// <summary>
    /// Builds options from configuration, reading the <c>Video</c> section key-by-key so the engine
    /// needs no configuration-binder package; missing keys keep their defaults.
    /// </summary>
    /// <param name="aConfiguration">The application configuration.</param>
    /// <returns>The populated options instance.</returns>
    public static VideoOptions FromConfiguration(IConfiguration aConfiguration)
    {
        var vOptions = new VideoOptions();
        if (bool.TryParse(aConfiguration["Video:LocalVideoModelInstalled"], out var vLocal)) { vOptions.LocalVideoModelInstalled = vLocal; }
        if (bool.TryParse(aConfiguration["Video:InterpolationModelInstalled"], out var vInterp)) { vOptions.InterpolationModelInstalled = vInterp; }
        if (bool.TryParse(aConfiguration["Video:UpscaleModelInstalled"], out var vUpscale)) { vOptions.UpscaleModelInstalled = vUpscale; }
        return vOptions;
    }
}
