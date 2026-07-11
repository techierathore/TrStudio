namespace TrStudio.Models.Contracts;

/// <summary>
/// Result of a caption/subtitle generation request (FN-040). Carries the rendered SRT/VTT track and
/// the cue count so an export can attach a valid, script-timed sidecar.
/// </summary>
public class SubtitleResponse
{
    /// <summary>The rendered caption track (SRT or WebVTT); null when the script was empty.</summary>
    public string? Content { get; set; }

    /// <summary>The output format echoed back (<c>srt</c> or <c>vtt</c>).</summary>
    public string Format { get; set; } = "srt";

    /// <summary>The number of caption cues produced.</summary>
    public int CueCount { get; set; }

    /// <summary>The total duration in seconds the cues span.</summary>
    public double DurationSeconds { get; set; }

    /// <summary>The language echoed back.</summary>
    public string Language { get; set; } = "en";
}
