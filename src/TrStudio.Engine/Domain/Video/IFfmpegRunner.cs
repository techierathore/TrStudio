namespace TrStudio.Engine.Domain.Video;

/// <summary>
/// The ffmpeg CLI seam used by the video editor (FN-054). Abstracted so the editor can be unit-tested
/// without a real ffmpeg binary and so the "not installed" degrade path is deterministic.
/// </summary>
public interface IFfmpegRunner
{
    /// <summary>Whether ffmpeg is available on the host PATH.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Runs an ffmpeg invocation with the given argument string.
    /// </summary>
    /// <param name="aArguments">The full ffmpeg argument string (excluding the executable name).</param>
    /// <param name="aCancellationToken">Token to cancel the invocation.</param>
    /// <returns>The invocation outcome (exit code + stderr); never throws for a missing binary.</returns>
    Task<FfmpegResult> RunAsync(string aArguments, CancellationToken aCancellationToken = default);
}
