namespace TrStudio.Engine.Domain.Voice;

/// <summary>
/// Builds a deterministic placeholder WAV binary used by the synthetic voice path when no real
/// voice model is installed (FN-031 graceful degrade / test fixture). The audio is silent PCM sized
/// to the requested duration so downstream timing metadata (FN-035) is realistic.
/// </summary>
internal static class SyntheticAudio
{
    /// <summary>The sample rate (Hz) of the synthetic PCM.</summary>
    public const int SampleRateHz = 16000;

    private const short BitsPerSample = 16;
    private const short Channels = 1;

    /// <summary>
    /// Estimates a natural speaking duration for a piece of text (~14 characters/second, floored).
    /// </summary>
    /// <param name="aText">The text to be spoken.</param>
    /// <param name="aSpeed">Playback speed multiplier (higher = shorter).</param>
    /// <returns>The estimated duration in seconds, clamped to a sane range.</returns>
    public static double EstimateDurationSeconds(string aText, double aSpeed)
    {
        var vChars = string.IsNullOrEmpty(aText) ? 0 : aText.Length;
        var vSpeed = Math.Clamp(aSpeed <= 0 ? 1.0d : aSpeed, 0.5d, 2.0d);
        var vSeconds = (vChars / 14.0d) / vSpeed;
        return Math.Clamp(vSeconds, 0.5d, 120.0d);
    }

    /// <summary>
    /// Builds a valid silent WAV byte array of the given duration.
    /// </summary>
    /// <param name="aDurationSeconds">Duration in seconds.</param>
    /// <returns>A complete WAV file (44-byte header + silent PCM data).</returns>
    public static byte[] BuildSilentWav(double aDurationSeconds)
    {
        var vSeconds = Math.Clamp(aDurationSeconds, 0.1d, 120.0d);
        var vSampleCount = (int)(vSeconds * SampleRateHz);
        var vDataBytes = vSampleCount * (BitsPerSample / 8) * Channels;
        var vByteRate = SampleRateHz * Channels * (BitsPerSample / 8);
        var vBlockAlign = (short)(Channels * (BitsPerSample / 8));

        using var vStream = new MemoryStream(44 + vDataBytes);
        using var vWriter = new BinaryWriter(vStream);

        // RIFF header.
        vWriter.Write(new[] { 'R', 'I', 'F', 'F' });
        vWriter.Write(36 + vDataBytes);
        vWriter.Write(new[] { 'W', 'A', 'V', 'E' });

        // fmt chunk (PCM).
        vWriter.Write(new[] { 'f', 'm', 't', ' ' });
        vWriter.Write(16);
        vWriter.Write((short)1);
        vWriter.Write(Channels);
        vWriter.Write(SampleRateHz);
        vWriter.Write(vByteRate);
        vWriter.Write(vBlockAlign);
        vWriter.Write(BitsPerSample);

        // data chunk (silence).
        vWriter.Write(new[] { 'd', 'a', 't', 'a' });
        vWriter.Write(vDataBytes);
        vWriter.Write(new byte[vDataBytes]);

        vWriter.Flush();
        return vStream.ToArray();
    }
}
