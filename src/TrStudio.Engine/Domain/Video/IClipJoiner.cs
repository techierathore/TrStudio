namespace TrStudio.Engine.Domain.Video;

/// <summary>
/// Joins finished clips and reads frames on this computer, never in the cloud (REQ-FN-042,
/// REQ-FN-055). The Mac app uses Apple's AVFoundation, built into macOS and allowed inside the app
/// sandbox; the test host and the unit tests use ffmpeg or a stand-in. Every path is a local file.
/// </summary>
public interface IClipJoiner
{
    /// <summary>What does the joining, for messages and run details (e.g. "AVFoundation", "ffmpeg").</summary>
    string Name { get; }

    /// <summary>Whether this joiner can run on this computer.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Joins clips end to end into one MP4, at the first clip's frame size; a clip of another size is
    /// scaled to fit. Audio is kept when the clips carry it.
    /// </summary>
    /// <param name="aInputPaths">The clips, in order (at least two).</param>
    /// <param name="aOutputPath">Where the joined MP4 is written.</param>
    /// <param name="aCancellationToken">Token to cancel the join.</param>
    /// <returns>The outcome with the joined clip's length and size.</returns>
    Task<ClipJoinResult> JoinAsync(IReadOnlyList<string> aInputPaths, string aOutputPath, CancellationToken aCancellationToken = default);

    /// <summary>Reads a clip's last frame as PNG bytes (the start picture of the next shot).</summary>
    /// <param name="aVideoPath">The clip.</param>
    /// <param name="aCancellationToken">Token to cancel the read.</param>
    /// <returns>The PNG bytes.</returns>
    Task<byte[]> LastFrameAsync(string aVideoPath, CancellationToken aCancellationToken = default);

    /// <summary>Reads a clip's length in seconds.</summary>
    /// <param name="aVideoPath">The clip.</param>
    /// <param name="aCancellationToken">Token to cancel the read.</param>
    /// <returns>The length in seconds.</returns>
    Task<double> DurationSecondsAsync(string aVideoPath, CancellationToken aCancellationToken = default);
}

/// <summary>The outcome of one join.</summary>
/// <param name="Ok">Whether the joined file was written.</param>
/// <param name="DurationSeconds">The joined clip's length.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
/// <param name="Problem">Why it failed, in plain words, when it did.</param>
public sealed record ClipJoinResult(bool Ok, double DurationSeconds, int Width, int Height, string? Problem);
