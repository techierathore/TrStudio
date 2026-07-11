namespace TrStudio.Models.Contracts;

/// <summary>
/// Request to synthesize speech from text (FN-031/033/034/BRD-22..25). One "voice" control selects
/// a preset voice; the engine routes to the per-language model behind the scenes. Emotion/style/speed
/// are applied only where the resolved voice's capability descriptor supports them.
/// </summary>
public class VoiceSynthesizeRequest
{
    /// <summary>The text to speak (required).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The preset voice id from the catalog (FN-031).</summary>
    public string VoiceId { get; set; } = string.Empty;

    /// <summary>Target language: <c>en</c>, <c>hi</c> or <c>hinglish</c> (default <c>en</c>) — drives model routing (FN-033).</summary>
    public string Language { get; set; } = "en";

    /// <summary>Optional emotion label (FN-034); ignored/reported when the voice does not support it.</summary>
    public string? Emotion { get; set; }

    /// <summary>Optional delivery style (FN-034); ignored/reported when unsupported.</summary>
    public string? Style { get; set; }

    /// <summary>Playback speed multiplier (FN-034); 1.0 = normal. Clamped to a sane range.</summary>
    public double Speed { get; set; } = 1.0d;

    /// <summary>Optional owning project id (association only).</summary>
    public Guid? ProjectId { get; set; }
}
