using System.Text;
using TrStudio.Models.Contracts;

namespace TrStudio.Engine.Domain.Avatar;

/// <summary>
/// Builds EN/HI captions/subtitles from a script as a valid, script-timed SRT or WebVTT sidecar
/// (FN-040/BRD-31). The script is segmented into cues on sentence boundaries and the cue timings are
/// distributed proportionally to each cue's spoken length across the clip duration, so the exported
/// track's timing matches the script. Deterministic — the same script + duration always renders the
/// same track (unit-testable).
/// </summary>
public static class AvatarCaptionBuilder
{
    private static readonly char[] SentenceTerminators = { '.', '!', '?', '\n', '।' };

    /// <summary>
    /// Renders a caption track from a subtitle request.
    /// </summary>
    /// <param name="aRequest">The subtitle request (script, language, duration, format).</param>
    /// <returns>The rendered SRT/VTT track plus cue metadata.</returns>
    public static SubtitleResponse Build(SubtitleRequest aRequest)
    {
        var vLanguage = string.IsNullOrWhiteSpace(aRequest.Language) ? "en" : aRequest.Language.Trim().ToLowerInvariant();
        var vFormat = string.Equals(aRequest.Format, "vtt", StringComparison.OrdinalIgnoreCase) ? "vtt" : "srt";

        var vSegments = Segment(aRequest.Script);
        if (vSegments.Count == 0)
        {
            return new SubtitleResponse { Content = null, Format = vFormat, CueCount = 0, DurationSeconds = 0, Language = vLanguage };
        }

        var vDuration = aRequest.DurationSeconds is { } vRequested && vRequested > 0
            ? vRequested
            : EstimateDuration(aRequest.Script);

        var vTotalWeight = vSegments.Sum(aSegment => Math.Max(1, aSegment.Length));
        var vContent = vFormat == "vtt" ? RenderVtt(vSegments, vDuration, vTotalWeight) : RenderSrt(vSegments, vDuration, vTotalWeight);

        return new SubtitleResponse
        {
            Content = vContent,
            Format = vFormat,
            CueCount = vSegments.Count,
            DurationSeconds = Math.Round(vDuration, 3),
            Language = vLanguage
        };
    }

    private static List<string> Segment(string aScript)
        => string.IsNullOrWhiteSpace(aScript)
            ? new List<string>()
            : aScript
                .Split(SentenceTerminators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(aSegment => aSegment.Length > 0)
                .ToList();

    private static double EstimateDuration(string aScript)
    {
        // ~14 characters/second natural speaking pace (mirrors SyntheticAudio), floored to a sane range.
        var vChars = aScript?.Length ?? 0;
        return Math.Clamp(vChars / 14.0d, 1.0d, 600.0d);
    }

    private static string RenderSrt(List<string> aSegments, double aDuration, int aTotalWeight)
    {
        var vBuilder = new StringBuilder();
        var vStart = 0.0d;
        for (var vIndex = 0; vIndex < aSegments.Count; vIndex++)
        {
            var vShare = (double)Math.Max(1, aSegments[vIndex].Length) / aTotalWeight;
            var vEnd = vIndex == aSegments.Count - 1 ? aDuration : vStart + vShare * aDuration;
            vBuilder.Append(vIndex + 1).Append('\n');
            vBuilder.Append(FormatSrtTime(vStart)).Append(" --> ").Append(FormatSrtTime(vEnd)).Append('\n');
            vBuilder.Append(aSegments[vIndex]).Append('\n').Append('\n');
            vStart = vEnd;
        }

        return vBuilder.ToString().TrimEnd('\n');
    }

    private static string RenderVtt(List<string> aSegments, double aDuration, int aTotalWeight)
    {
        var vBuilder = new StringBuilder();
        vBuilder.Append("WEBVTT").Append('\n').Append('\n');
        var vStart = 0.0d;
        for (var vIndex = 0; vIndex < aSegments.Count; vIndex++)
        {
            var vShare = (double)Math.Max(1, aSegments[vIndex].Length) / aTotalWeight;
            var vEnd = vIndex == aSegments.Count - 1 ? aDuration : vStart + vShare * aDuration;
            vBuilder.Append(FormatVttTime(vStart)).Append(" --> ").Append(FormatVttTime(vEnd)).Append('\n');
            vBuilder.Append(aSegments[vIndex]).Append('\n').Append('\n');
            vStart = vEnd;
        }

        return vBuilder.ToString().TrimEnd('\n');
    }

    private static string FormatSrtTime(double aSeconds)
    {
        var vSpan = TimeSpan.FromSeconds(Math.Max(0, aSeconds));
        return $"{(int)vSpan.TotalHours:00}:{vSpan.Minutes:00}:{vSpan.Seconds:00},{vSpan.Milliseconds:000}";
    }

    private static string FormatVttTime(double aSeconds)
    {
        var vSpan = TimeSpan.FromSeconds(Math.Max(0, aSeconds));
        return $"{(int)vSpan.TotalHours:00}:{vSpan.Minutes:00}:{vSpan.Seconds:00}.{vSpan.Milliseconds:000}";
    }
}
