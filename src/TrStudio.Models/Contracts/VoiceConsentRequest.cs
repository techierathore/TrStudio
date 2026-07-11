namespace TrStudio.Models.Contracts;

/// <summary>
/// Records recorded-consent evidence for cloning a real person's voice (FN-032 / SAFE-03 /
/// REQ-FN-058). Persisting this evidence is what unblocks a subsequent real-person clone.
/// </summary>
public class VoiceConsentRequest
{
    /// <summary>Stable reference identifying the real subject the consent covers (required).</summary>
    public string SubjectReference { get; set; } = string.Empty;

    /// <summary>The kind of evidence (e.g. <c>signed-release</c>, <c>recorded-verbal</c>).</summary>
    public string EvidenceType { get; set; } = string.Empty;

    /// <summary>A description, URI or hash pointing at the stored evidence (audit trail, SAFE-06).</summary>
    public string EvidenceDetail { get; set; } = string.Empty;

    /// <summary>Whether consent was granted (false records an explicit refusal).</summary>
    public bool ConsentGranted { get; set; } = true;
}
