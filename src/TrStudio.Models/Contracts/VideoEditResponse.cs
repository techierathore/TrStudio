namespace TrStudio.Models.Contracts;

/// <summary>
/// Response for a video edit + export (FN-054). Reports the applied operations, the export format and
/// resolved dimensions, whether ffmpeg was available (real edit) or the request degraded cleanly, and
/// the produced MP4 asset id when the export ran for real.
/// </summary>
public class VideoEditResponse
{
    /// <summary>Outcome token: <c>ok</c>, <c>degraded</c> or <c>error</c>.</summary>
    public string Status { get; set; } = "ok";

    /// <summary>Whether ffmpeg was available on the host (a real edit ran) or the request degraded.</summary>
    public bool FfmpegAvailable { get; set; }

    /// <summary>Whether the edit degraded cleanly (ffmpeg not installed).</summary>
    public bool Degraded { get; set; }

    /// <summary>The human-readable list of applied operations (trim/join/reframe/add-audio/export).</summary>
    public List<string> Operations { get; set; } = new();

    /// <summary>The export aspect format applied.</summary>
    public string ExportFormat { get; set; } = string.Empty;

    /// <summary>The exported clip pixel width, when the export ran for real.</summary>
    public int? Width { get; set; }

    /// <summary>The exported clip pixel height, when the export ran for real.</summary>
    public int? Height { get; set; }

    /// <summary>The produced MP4 asset id, when the export ran for real.</summary>
    public Guid? OutputAssetId { get; set; }

    /// <summary>An actionable human-readable message describing the outcome/degrade.</summary>
    public string? Message { get; set; }
}
