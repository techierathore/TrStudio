namespace TrStudio.Models.Contracts;

/// <summary>
/// Result of recording voice-clone consent evidence (FN-032 / SAFE-03).
/// </summary>
public class VoiceConsentResponse
{
    /// <summary>The persisted evidence id.</summary>
    public Guid EvidenceId { get; set; }

    /// <summary>The subject reference the evidence covers.</summary>
    public string SubjectReference { get; set; } = string.Empty;

    /// <summary>Whether the evidence was recorded.</summary>
    public bool Recorded { get; set; }

    /// <summary>A human-readable confirmation message.</summary>
    public string Message { get; set; } = string.Empty;
}
