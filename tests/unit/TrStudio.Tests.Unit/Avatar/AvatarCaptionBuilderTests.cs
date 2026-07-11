using System.Text.RegularExpressions;
using TrStudio.Engine.Domain.Avatar;
using TrStudio.Models.Contracts;

namespace TrStudio.Tests.Unit.Avatar;

/// <summary>
/// Unit tests for <see cref="AvatarCaptionBuilder"/> (FN-040/BRD-31): a valid, script-timed SRT/VTT
/// sidecar whose cues cover the clip duration and match the script segmentation.
/// </summary>
public class AvatarCaptionBuilderTests
{
    private const string SrtCuePattern =
        @"^\d+\s*\n\d{2}:\d{2}:\d{2},\d{3} --> \d{2}:\d{2}:\d{2},\d{3}";

    /// <summary>A multi-sentence script renders one SRT cue per sentence with valid timecodes.</summary>
    [Fact]
    public void BuildsValidSrtWithCuePerSentence()
    {
        var vResponse = AvatarCaptionBuilder.Build(new SubtitleRequest
        {
            Script = "Hello there. This is TrStudio. Welcome to the avatar studio.",
            Language = "en",
            DurationSeconds = 12,
            Format = "srt"
        });

        Assert.Equal("srt", vResponse.Format);
        Assert.Equal(3, vResponse.CueCount);
        Assert.NotNull(vResponse.Content);
        Assert.Matches(SrtCuePattern, vResponse.Content);

        // The first cue starts at 0 and the last cue ends exactly at the clip duration.
        Assert.Contains("00:00:00,000 -->", vResponse.Content);
        Assert.Contains("00:00:12,000", vResponse.Content);
    }

    /// <summary>The cue timings are monotonic and span the whole requested duration.</summary>
    [Fact]
    public void CueTimingsAreMonotonicAndCoverDuration()
    {
        var vResponse = AvatarCaptionBuilder.Build(new SubtitleRequest
        {
            Script = "One. Two. Three. Four.",
            DurationSeconds = 8,
            Format = "srt"
        });

        var vTimes = Regex.Matches(vResponse.Content!, @"(\d{2}):(\d{2}):(\d{2}),(\d{3})")
            .Select(aMatch => TimeSpan.Parse($"{aMatch.Groups[1].Value}:{aMatch.Groups[2].Value}:{aMatch.Groups[3].Value}").TotalSeconds
                              + int.Parse(aMatch.Groups[4].Value) / 1000.0)
            .ToList();

        for (var vIndex = 1; vIndex < vTimes.Count; vIndex++)
        {
            Assert.True(vTimes[vIndex] >= vTimes[vIndex - 1], "SRT timecodes must be non-decreasing.");
        }

        Assert.Equal(8.0, vTimes.Last(), 3);
    }

    /// <summary>Hindi output is supported and the language echoes back.</summary>
    [Fact]
    public void SupportsHindiLanguage()
    {
        var vResponse = AvatarCaptionBuilder.Build(new SubtitleRequest
        {
            Script = "नमस्ते। यह ट्रस्टूडियो है।",
            Language = "hi",
            Format = "srt"
        });

        Assert.Equal("hi", vResponse.Language);
        Assert.Equal(2, vResponse.CueCount);
    }

    /// <summary>VTT output carries the WEBVTT header and dotted-millisecond timecodes.</summary>
    [Fact]
    public void BuildsWebVttWithHeader()
    {
        var vResponse = AvatarCaptionBuilder.Build(new SubtitleRequest
        {
            Script = "Alpha. Beta.",
            DurationSeconds = 4,
            Format = "vtt"
        });

        Assert.Equal("vtt", vResponse.Format);
        Assert.StartsWith("WEBVTT", vResponse.Content);
        Assert.Contains("00:00:00.000 -->", vResponse.Content);
    }

    /// <summary>An empty script produces no cues and a null track (no crash).</summary>
    [Fact]
    public void EmptyScriptProducesNoCues()
    {
        var vResponse = AvatarCaptionBuilder.Build(new SubtitleRequest { Script = "   ", Format = "srt" });

        Assert.Equal(0, vResponse.CueCount);
        Assert.Null(vResponse.Content);
    }
}
