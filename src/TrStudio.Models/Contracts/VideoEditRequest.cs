namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for a video edit + export (<c>POST /api/v1/video/edit</c>, FN-054/BRD-45): trim,
/// join, reframe, add-audio and export to MP4 in a platform aspect format. The primary clip and any
/// joined clips / added audio are owner-scoped assets (BRD-81). The edit runs for real via ffmpeg
/// when it is installed; otherwise it degrades cleanly with an actionable "ffmpeg not installed"
/// message rather than crashing.
/// </summary>
public class VideoEditRequest
{
    /// <summary>The owner-scoped primary source video asset.</summary>
    public Guid SourceAssetId { get; set; }

    /// <summary>Trim: start offset in seconds (0 = from the beginning). Ignored when null.</summary>
    public double? TrimStartSeconds { get; set; }

    /// <summary>Trim: end offset in seconds. Ignored when null (runs to the end).</summary>
    public double? TrimEndSeconds { get; set; }

    /// <summary>Join: additional owner-scoped clips concatenated after the primary clip, in order.</summary>
    public List<Guid> JoinAssetIds { get; set; } = new();

    /// <summary>Add-audio: an owner-scoped Voice-studio audio asset muxed onto the output track.</summary>
    public Guid? AudioAssetId { get; set; }

    /// <summary>
    /// Export aspect format: one of <c>16:9</c>, <c>9:16</c>, <c>1:1</c>, <c>4:5</c>. The output is
    /// reframed (scale + pad) to the chosen platform format and exported as MP4 (H.264/AAC).
    /// </summary>
    public string ExportFormat { get; set; } = "16:9";

    /// <summary>The project to attach the exported asset to, if any.</summary>
    public Guid? ProjectId { get; set; }
}
