namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for a video post-processing step (<c>POST /api/v1/video/interpolate</c> and
/// <c>/upscale</c>, FN-053/BRD-44). The source clip is an owner-scoped video asset; the step is
/// applied as a structural post-step that degrades cleanly when its model (RIFE/FILM for
/// interpolation, an upscale network) is not installed.
/// </summary>
public class VideoPostStepRequest
{
    /// <summary>The owner-scoped source video asset to post-process.</summary>
    public Guid SourceAssetId { get; set; }

    /// <summary>Frame interpolation: the target multiplier (e.g. 2 = double the frame rate). Interpolate only.</summary>
    public int InterpolationFactor { get; set; } = 2;

    /// <summary>Upscale: the target spatial scale factor (e.g. 2 = 2x). Upscale only.</summary>
    public int ScaleFactor { get; set; } = 2;

    /// <summary>The project to attach the produced asset to, if any.</summary>
    public Guid? ProjectId { get; set; }
}
