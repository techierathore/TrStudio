using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace TrStudio.Engine.Domain.Video;

/// <summary>
/// The ffmpeg <see cref="IClipJoiner"/> (REQ-FN-055): joins clips on this computer with a concat
/// filter that scales and pads every clip to the first clip's size and frame rate, keeps the sound
/// when every clip has one, and writes H.264/AAC in an MP4. The test host and the unit tests use it;
/// the Mac app uses AVFoundation instead because a sandboxed app cannot rely on running ffmpeg.
/// </summary>
public sealed partial class FfmpegClipJoiner : IClipJoiner
{
    private readonly IFfmpegRunner objFfmpeg;
    private readonly ILogger<FfmpegClipJoiner> objLogger;

    /// <summary>Initialises the joiner.</summary>
    /// <param name="aFfmpeg">The ffmpeg runner.</param>
    /// <param name="aLogger">The logger.</param>
    public FfmpegClipJoiner(IFfmpegRunner aFfmpeg, ILogger<FfmpegClipJoiner> aLogger)
    {
        objFfmpeg = aFfmpeg;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public string Name => "ffmpeg";

    /// <inheritdoc />
    public bool IsAvailable => objFfmpeg.IsAvailable;

    /// <inheritdoc />
    public async Task<ClipJoinResult> JoinAsync(IReadOnlyList<string> aInputPaths, string aOutputPath, CancellationToken aCancellationToken = default)
    {
        if (aInputPaths.Count < 2)
        {
            return new ClipJoinResult(false, 0, 0, 0, "Joining needs at least two clips.");
        }

        if (!IsAvailable)
        {
            return new ClipJoinResult(false, 0, 0, 0, "ffmpeg is not installed on this computer, so clips cannot be joined here.");
        }

        var vProbes = new List<ClipProbe>();
        foreach (var vPath in aInputPaths)
        {
            var vProbe = await ProbeAsync(vPath, aCancellationToken).ConfigureAwait(false);
            if (vProbe is null)
            {
                return new ClipJoinResult(false, 0, 0, 0, $"A clip could not be read: {Path.GetFileName(vPath)}.");
            }

            vProbes.Add(vProbe);
        }

        var vWidth = Math.Max(2, vProbes[0].Width / 2 * 2);
        var vHeight = Math.Max(2, vProbes[0].Height / 2 * 2);
        var vFps = vProbes[0].Fps > 0 ? vProbes[0].Fps : 24;
        var vWithAudio = vProbes.All(aProbe => aProbe.HasAudio);

        var vArgs = new StringBuilder("-y ");
        foreach (var vPath in aInputPaths)
        {
            vArgs.Append(CultureInfo.InvariantCulture, $"-i \"{vPath}\" ");
        }

        var vFilter = new StringBuilder();
        for (var vIndex = 0; vIndex < aInputPaths.Count; vIndex++)
        {
            vFilter.Append(CultureInfo.InvariantCulture,
                $"[{vIndex}:v]scale={vWidth}:{vHeight}:force_original_aspect_ratio=decrease,pad={vWidth}:{vHeight}:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1,fps={vFps.ToString("0.###", CultureInfo.InvariantCulture)},format=yuv420p[v{vIndex}];");
            if (vWithAudio)
            {
                vFilter.Append(CultureInfo.InvariantCulture,
                    $"[{vIndex}:a]aresample=44100,aformat=sample_fmts=fltp:channel_layouts=stereo[a{vIndex}];");
            }
        }

        for (var vIndex = 0; vIndex < aInputPaths.Count; vIndex++)
        {
            vFilter.Append(CultureInfo.InvariantCulture, $"[v{vIndex}]");
            if (vWithAudio)
            {
                vFilter.Append(CultureInfo.InvariantCulture, $"[a{vIndex}]");
            }
        }

        vFilter.Append(CultureInfo.InvariantCulture, $"concat=n={aInputPaths.Count}:v=1:a={(vWithAudio ? 1 : 0)}[outv]{(vWithAudio ? "[outa]" : string.Empty)}");
        vArgs.Append(CultureInfo.InvariantCulture, $"-filter_complex \"{vFilter}\" -map \"[outv]\" ");
        vArgs.Append(vWithAudio ? "-map \"[outa]\" -c:a aac " : "-an ");
        vArgs.Append(CultureInfo.InvariantCulture, $"-c:v libx264 -preset veryfast -pix_fmt yuv420p -movflags +faststart \"{aOutputPath}\"");

        var vRun = await objFfmpeg.RunAsync(vArgs.ToString(), aCancellationToken).ConfigureAwait(false);
        if (!vRun.Succeeded || !File.Exists(aOutputPath))
        {
            objLogger.LogWarning("ffmpeg join failed (exit {ExitCode})", vRun.ExitCode);
            return new ClipJoinResult(false, 0, 0, 0, "The clips could not be joined.");
        }

        var vSeconds = await DurationSecondsAsync(aOutputPath, aCancellationToken).ConfigureAwait(false);
        return new ClipJoinResult(true, vSeconds, vWidth, vHeight, null);
    }

    /// <inheritdoc />
    public async Task<byte[]> LastFrameAsync(string aVideoPath, CancellationToken aCancellationToken = default)
    {
        var vTemp = Path.Combine(Path.GetTempPath(), $"trstudio-lastframe-{Guid.NewGuid():N}.png");
        try
        {
            var vRun = await objFfmpeg.RunAsync($"-y -sseof -0.1 -i \"{aVideoPath}\" -frames:v 1 \"{vTemp}\"", aCancellationToken).ConfigureAwait(false);
            if (!vRun.Succeeded || !File.Exists(vTemp))
            {
                throw new InvalidOperationException("The last picture of the clip could not be read.");
            }

            return await File.ReadAllBytesAsync(vTemp, aCancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TryDelete(vTemp);
        }
    }

    /// <inheritdoc />
    public async Task<double> DurationSecondsAsync(string aVideoPath, CancellationToken aCancellationToken = default)
    {
        var vProbe = await ProbeAsync(aVideoPath, aCancellationToken).ConfigureAwait(false);
        return vProbe?.DurationSeconds ?? throw new InvalidOperationException("The clip's length could not be read.");
    }

    private async Task<ClipProbe?> ProbeAsync(string aPath, CancellationToken aCancellationToken)
    {
        if (!File.Exists(aPath))
        {
            return null;
        }

        var vRun = await objFfmpeg.RunAsync($"-hide_banner -i \"{aPath}\"", aCancellationToken).ConfigureAwait(false);
        if (!vRun.Available)
        {
            return null;
        }

        var vDuration = DurationPattern().Match(vRun.StdErr);
        if (!vDuration.Success)
        {
            return null;
        }

        var vSeconds = int.Parse(vDuration.Groups[1].Value, CultureInfo.InvariantCulture) * 3600d
            + int.Parse(vDuration.Groups[2].Value, CultureInfo.InvariantCulture) * 60d
            + double.Parse(vDuration.Groups[3].Value, CultureInfo.InvariantCulture);

        var vSize = SizePattern().Match(vRun.StdErr);
        var vFps = FpsPattern().Match(vRun.StdErr);
        return new ClipProbe(
            vSeconds,
            vSize.Success ? int.Parse(vSize.Groups[1].Value, CultureInfo.InvariantCulture) : 0,
            vSize.Success ? int.Parse(vSize.Groups[2].Value, CultureInfo.InvariantCulture) : 0,
            vFps.Success ? double.Parse(vFps.Groups[1].Value, CultureInfo.InvariantCulture) : 0,
            vRun.StdErr.Contains("Audio:", StringComparison.Ordinal));
    }

    private static void TryDelete(string aPath)
    {
        try
        {
            if (File.Exists(aPath))
            {
                File.Delete(aPath);
            }
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    [GeneratedRegex(@"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)")]
    private static partial Regex DurationPattern();

    [GeneratedRegex(@"Video:.*?[ ,](\d{2,5})x(\d{2,5})[ ,\[]")]
    private static partial Regex SizePattern();

    [GeneratedRegex(@"(\d+(?:\.\d+)?) fps")]
    private static partial Regex FpsPattern();

    private sealed record ClipProbe(double DurationSeconds, int Width, int Height, double Fps, bool HasAudio);
}
