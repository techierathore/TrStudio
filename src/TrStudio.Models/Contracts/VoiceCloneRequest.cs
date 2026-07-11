namespace TrStudio.Models.Contracts;

/// <summary>
/// Request to register a cloned voice (FN-032/BRD-23). Cloning a REAL person is consent-gated
/// (SAFE-03 / REQ-FN-058): it is blocked until recorded-consent evidence exists for the subject.
/// Fully-synthetic/preset-derived voices are never gated.
/// </summary>
public class VoiceCloneRequest
{
    /// <summary>A name for the cloned voice (required).</summary>
    public string VoiceName { get; set; } = string.Empty;

    /// <summary>Target language: <c>en</c>, <c>hi</c> or <c>hinglish</c> (default <c>en</c>).</summary>
    public string Language { get; set; } = "en";

    /// <summary>Whether the clone reproduces a real person's voice (triggers the consent gate).</summary>
    public bool IsRealPerson { get; set; }

    /// <summary>Stable reference identifying the real subject, used to look up consent evidence.</summary>
    public string? SubjectReference { get; set; }

    /// <summary>Optional reference to the uploaded voice sample (not processed here; model training is deferred).</summary>
    public string? SampleReference { get; set; }
}
