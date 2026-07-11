using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for a video generation job (<c>POST /api/v1/video/generate</c>, FN-051/BRD-42).
/// Covers both image-to-video (I2V) and text-to-video (T2V): <see cref="Mode"/> selects the task and
/// the tier-routed model behind it. Duration / motion / aspect are the creator controls; the
/// FN-052 conditioning asset references (first/last frame + reference image) drive consistency and
/// are owner-scoped when supplied.
/// </summary>
public class VideoGenerateRequest
{
    /// <summary>The generation mode: <c>i2v</c> (image-to-video) or <c>t2v</c> (text-to-video).</summary>
    public string Mode { get; set; } = "t2v";

    /// <summary>The text prompt driving the clip (required for T2V; optional motion prompt for I2V).</summary>
    public string? Prompt { get; set; }

    /// <summary>The execution tier: Fast (local), High (RunPod) or Premium (closed aggregator).</summary>
    public GenerationTier Tier { get; set; } = GenerationTier.Fast;

    /// <summary>Requested clip duration in seconds (clamped to the model's supported range).</summary>
    public double DurationSeconds { get; set; } = 4d;

    /// <summary>Motion intensity control (0 = still, 10 = maximum motion); clamped server-side.</summary>
    public double Motion { get; set; } = 5d;

    /// <summary>Aspect ratio of the generated clip: one of <c>16:9</c>, <c>9:16</c>, <c>1:1</c>, <c>4:5</c>.</summary>
    public string AspectRatio { get; set; } = "16:9";

    /// <summary>For I2V: the owner-scoped source image asset the clip animates from.</summary>
    public Guid? SourceImageAssetId { get; set; }

    /// <summary>FN-052: owner-scoped first-frame conditioning image for temporal consistency.</summary>
    public Guid? FirstFrameAssetId { get; set; }

    /// <summary>FN-052: owner-scoped last-frame conditioning image for temporal consistency.</summary>
    public Guid? LastFrameAssetId { get; set; }

    /// <summary>FN-052: owner-scoped reference image (character/style consistency across the clip).</summary>
    public Guid? ReferenceAssetId { get; set; }

    /// <summary>The project to attach the job to, if any.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Explicit random seed for reproducibility; a random seed is used when null.</summary>
    public long? Seed { get; set; }
}
