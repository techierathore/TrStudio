using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// Records recorded-consent evidence for real-person face OR voice work (SAFE-03 / BRD-62 /
/// REQ-FN-058). Persisting a granted record is what unblocks generations that depict the subject.
/// </summary>
public class ConsentRecordRequest
{
    /// <summary>Stable reference identifying the real subject the consent covers (required).</summary>
    public string SubjectReference { get; set; } = string.Empty;

    /// <summary>Whether the consent covers the subject's face or voice.</summary>
    public ConsentModality Modality { get; set; }

    /// <summary>The kind of evidence (e.g. <c>signed-release</c>, <c>recorded-verbal</c>).</summary>
    public string EvidenceType { get; set; } = string.Empty;

    /// <summary>A description, URI or hash pointing at the stored evidence (audit trail, SAFE-06).</summary>
    public string EvidenceDetail { get; set; } = string.Empty;

    /// <summary>Whether consent was granted (false records an explicit refusal).</summary>
    public bool ConsentGranted { get; set; } = true;
}
