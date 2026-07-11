using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TrStudio.Engine.Domain.Video;

/// <summary>
/// Thin seam over the <c>ffmpeg</c> CLI used by the video editor (FN-054). It probes whether ffmpeg
/// is on the host PATH (cached after the first check) and runs an ffmpeg invocation, capturing the
/// exit code and stderr. When ffmpeg is absent the editor degrades cleanly with an actionable
/// message rather than crashing — this runner never throws for a missing binary, it reports
/// <see cref="IsAvailable"/> = <c>false</c>.
/// </summary>
public class FfmpegRunner : IFfmpegRunner
{
    private const string ExecutableName = "ffmpeg";

    private readonly ILogger<FfmpegRunner> objLogger;
    private readonly Lazy<bool> objAvailable;

    /// <summary>
    /// Initialises the runner and defers the availability probe until first use.
    /// </summary>
    /// <param name="aLogger">Structured logger.</param>
    public FfmpegRunner(ILogger<FfmpegRunner> aLogger)
    {
        objLogger = aLogger;
        objAvailable = new Lazy<bool>(Probe);
    }

    /// <inheritdoc />
    public bool IsAvailable => objAvailable.Value;

    /// <inheritdoc />
    public async Task<FfmpegResult> RunAsync(string aArguments, CancellationToken aCancellationToken = default)
    {
        if (!IsAvailable)
        {
            return new FfmpegResult(false, -1, "ffmpeg is not installed on the host PATH.");
        }

        try
        {
            using var vProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ExecutableName,
                    Arguments = aArguments,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            vProcess.Start();
            var vStdErr = await vProcess.StandardError.ReadToEndAsync(aCancellationToken).ConfigureAwait(false);
            await vProcess.WaitForExitAsync(aCancellationToken).ConfigureAwait(false);
            return new FfmpegResult(true, vProcess.ExitCode, vStdErr);
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "ffmpeg invocation failed");
            return new FfmpegResult(true, -1, vException.Message);
        }
    }

    private bool Probe()
    {
        try
        {
            using var vProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ExecutableName,
                    Arguments = "-version",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            vProcess.Start();
            vProcess.WaitForExit(5000);
            return vProcess.HasExited && vProcess.ExitCode == 0;
        }
        catch (Exception vException)
        {
            objLogger.LogInformation(vException, "ffmpeg not detected on the host PATH; video edits will degrade gracefully");
            return false;
        }
    }
}

/// <summary>The outcome of an ffmpeg invocation.</summary>
/// <param name="Available">Whether ffmpeg was available to run.</param>
/// <param name="ExitCode">The process exit code (0 = success); -1 when it could not run.</param>
/// <param name="StdErr">The captured standard-error output (ffmpeg logs progress to stderr).</param>
public sealed record FfmpegResult(bool Available, int ExitCode, string StdErr)
{
    /// <summary>Whether the invocation ran and exited successfully.</summary>
    public bool Succeeded => Available && ExitCode == 0;
}
