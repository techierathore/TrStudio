namespace TrStudio.Models.Contracts;

/// <summary>
/// Request to generate EN/HI captions/subtitles from a script (FN-040/BRD-31). The script is
/// segmented into timed cues distributed across the clip duration; the result is a valid SRT
/// (sidecar) whose timing matches the script.
/// </summary>
public class SubtitleRequest
{
    /// <summary>The script text to caption (required).</summary>
    public string Script { get; set; } = string.Empty;

    /// <summary>Target language (<c>en</c>/<c>hi</c>/<c>hinglish</c>); defaults to <c>en</c>.</summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// The clip duration in seconds the cues are distributed across. When null it is estimated from
    /// the script length so the SRT timing still matches the spoken pace.
    /// </summary>
    public double? DurationSeconds { get; set; }

    /// <summary>Output format: <c>srt</c> (timed sidecar) or <c>vtt</c> (WebVTT). Defaults to <c>srt</c>.</summary>
    public string Format { get; set; } = "srt";
}
