namespace TrStudio.Models.Contracts;

/// <summary>
/// A preset voice as exposed to creators (FN-031/BRD-22). The capability flags drive which
/// emotion/style/speed controls the UI enables (FN-034); the routed model is hidden behind the
/// single "voice" control (FN-033) but reported here for transparency.
/// </summary>
public class PresetVoiceResponse
{
    /// <summary>Stable preset voice id.</summary>
    public string VoiceId { get; set; } = string.Empty;

    /// <summary>Human-readable display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Voice language: <c>en</c>, <c>hi</c> or <c>hinglish</c>.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Perceived gender label (for pickers).</summary>
    public string Gender { get; set; } = string.Empty;

    /// <summary>Whether this is a fully-synthetic voice (never consent-gated).</summary>
    public bool IsSynthetic { get; set; }

    /// <summary>The per-language model this voice routes to (FN-033).</summary>
    public string RoutedModel { get; set; } = string.Empty;

    /// <summary>Whether emotion control is supported (FN-034).</summary>
    public bool SupportsEmotion { get; set; }

    /// <summary>Whether delivery-style control is supported (FN-034).</summary>
    public bool SupportsStyle { get; set; }

    /// <summary>Whether speed control is supported (FN-034).</summary>
    public bool SupportsSpeed { get; set; }

    /// <summary>The emotion labels the voice offers, when supported.</summary>
    public string[] Emotions { get; set; } = Array.Empty<string>();
}
