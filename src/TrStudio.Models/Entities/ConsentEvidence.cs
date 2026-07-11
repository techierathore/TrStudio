using TrStudio.Models.Enums;

namespace TrStudio.Models.Entities;

/// <summary>
/// Recorded-consent evidence authorising real-person face OR voice work (SAFE-03 / BRD-62 /
/// REQ-FN-058). Generalises the voice-only consent record so the shared consent gate covers both
/// modalities. Owned by the AppManager user who recorded it (BRD-81); forms part of the
/// non-disableable audit trail (SAFE-06). Persisting a granted row is what unblocks the subject.
/// </summary>
public class ConsentEvidence
{
    /// <summary>Unique evidence id.</summary>
    public Guid EvidenceId { get; set; }

    /// <summary>The AppManager user id that recorded the evidence (owner filter, BRD-81).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Stable reference identifying the real subject the consent covers.</summary>
    public string SubjectReference { get; set; } = string.Empty;

    /// <summary>Whether the evidence covers the subject's face or voice.</summary>
    public ConsentModality Modality { get; set; }

    /// <summary>The kind of evidence (e.g. <c>signed-release</c>, <c>recorded-verbal</c>).</summary>
    public string EvidenceType { get; set; } = string.Empty;

    /// <summary>A description, URI or hash pointing at the stored evidence.</summary>
    public string EvidenceDetail { get; set; } = string.Empty;

    /// <summary>Whether consent was granted (false records an explicit refusal).</summary>
    public bool ConsentGranted { get; set; }

    /// <summary>UTC timestamp when the evidence was recorded.</summary>
    public DateTime CreatedUtc { get; set; }
}
