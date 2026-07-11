namespace TrStudio.Models.Entities;

/// <summary>
/// A preset voice descriptor in the built-in voice library (FN-031/BRD-22). Preset voices are
/// fully-synthetic and never consent-gated. The capability flags describe which emotion/style/speed
/// controls the voice honours (FN-034); the routed model is the per-language backend (FN-033).
/// </summary>
public class PresetVoice
{
    /// <summary>Stable preset voice id.</summary>
    public string VoiceId { get; set; } = string.Empty;

    /// <summary>Human-readable display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Voice language: <c>en</c>, <c>hi</c> or <c>hinglish</c>.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Perceived gender label (for pickers).</summary>
    public string Gender { get; set; } = string.Empty;

    /// <summary>Always true for preset voices (fully-synthetic).</summary>
    public bool IsSynthetic { get; set; } = true;

    /// <summary>Whether emotion control is supported (FN-034).</summary>
    public bool SupportsEmotion { get; set; }

    /// <summary>Whether delivery-style control is supported (FN-034).</summary>
    public bool SupportsStyle { get; set; }

    /// <summary>Whether speed control is supported (FN-034).</summary>
    public bool SupportsSpeed { get; set; }

    /// <summary>The emotion labels this voice offers when <see cref="SupportsEmotion"/> is set.</summary>
    public string[] Emotions { get; set; } = Array.Empty<string>();
}
