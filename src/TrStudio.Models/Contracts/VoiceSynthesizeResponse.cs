namespace TrStudio.Models.Contracts;

/// <summary>
/// Result of a TTS request (FN-031/033/034/BRD-22..25). When no real voice model is installed the
/// response degrades gracefully — an actionable "voice model not installed" message — while a
/// deterministic synthetic audio asset may still be produced for tests.
/// </summary>
public class VoiceSynthesizeResponse
{
    /// <summary>Whether the result came from a degraded/synthetic path rather than a live voice model.</summary>
    public bool Degraded { get; set; }

    /// <summary>The path that produced this result: <c>synthesized</c>, <c>synthetic-dev</c> or <c>model-not-installed</c>.</summary>
    public string Mode { get; set; } = "model-not-installed";

    /// <summary>An actionable message when degraded (e.g. how to install the model); otherwise null.</summary>
    public string? Message { get; set; }

    /// <summary>The persisted audio asset id when audio was produced; otherwise null.</summary>
    public Guid? AssetId { get; set; }

    /// <summary>The storage path of the produced audio, when any.</summary>
    public string? StoragePath { get; set; }

    /// <summary>The per-language model the "voice" control routed to (FN-033).</summary>
    public string ResolvedModel { get; set; } = string.Empty;

    /// <summary>The requested language echoed back.</summary>
    public string Language { get; set; } = "en";

    /// <summary>The preset voice id echoed back.</summary>
    public string VoiceId { get; set; } = string.Empty;

    /// <summary>Controls that were applied (subset of emotion/style/speed) — FN-034.</summary>
    public List<string> AppliedControls { get; set; } = new();

    /// <summary>Requested controls the resolved voice does not support, reported/disabled — FN-034.</summary>
    public List<string> UnsupportedControls { get; set; } = new();

    /// <summary>Duration (seconds) of the produced audio — timing metadata for the avatar handoff (FN-035).</summary>
    public double DurationSeconds { get; set; }
}
