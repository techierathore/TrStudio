namespace TrStudio.Models.Entities;

/// <summary>
/// Recorded-consent evidence authorising a real-person voice clone (FN-032 / SAFE-03 / REQ-FN-058).
/// Owned by the AppManager user who recorded it; forms part of the non-disableable audit trail
/// (SAFE-06). Persistence of a granted row is what unblocks a real-person clone for that subject.
/// </summary>
public class VoiceConsentEvidence
{
    /// <summary>Unique evidence id.</summary>
    public Guid EvidenceId { get; set; }

    /// <summary>The AppManager user id that recorded the evidence (owner filter, BRD-81).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Stable reference identifying the real subject the consent covers.</summary>
    public string SubjectReference { get; set; } = string.Empty;

    /// <summary>The kind of evidence (e.g. <c>signed-release</c>, <c>recorded-verbal</c>).</summary>
    public string EvidenceType { get; set; } = string.Empty;

    /// <summary>A description, URI or hash pointing at the stored evidence.</summary>
    public string EvidenceDetail { get; set; } = string.Empty;

    /// <summary>Whether consent was granted (false records an explicit refusal).</summary>
    public bool ConsentGranted { get; set; }

    /// <summary>UTC timestamp when the evidence was recorded.</summary>
    public DateTime CreatedUtc { get; set; }
}
