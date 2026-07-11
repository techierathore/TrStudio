namespace TrStudio.Models.Contracts;

/// <summary>
/// The result of recording consent evidence via the shared consent service (SAFE-03 / BRD-62).
/// </summary>
public class ConsentRecordResponse
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
