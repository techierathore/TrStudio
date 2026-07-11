namespace TrStudio.Models.Entities;

/// <summary>
/// A persisted abuse report (SAFE-06 / BRD-65). Any caller can report content or a generation for
/// review; the report is retained for the deployment operator. Owner-scoped by the reporting
/// AppManager user id (BRD-81).
/// </summary>
public class AbuseReport
{
    /// <summary>Unique report identifier.</summary>
    public Guid ReportId { get; set; }

    /// <summary>The AppManager user id that filed the report (owner filter, BRD-81).</summary>
    public string ReporterUserId { get; set; } = string.Empty;

    /// <summary>What is being reported (e.g. <c>asset</c>, <c>job</c>, <c>character</c>).</summary>
    public string TargetKind { get; set; } = string.Empty;

    /// <summary>A reference identifying the reported target (an id or descriptor).</summary>
    public string TargetReference { get; set; } = string.Empty;

    /// <summary>The abuse category (e.g. <c>csam</c>, <c>ncii</c>, <c>impersonation</c>, <c>other</c>).</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Free-text details supplied by the reporter.</summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>The triage status (defaults to <c>open</c>).</summary>
    public string Status { get; set; } = "open";

    /// <summary>UTC timestamp when the report was filed.</summary>
    public DateTime CreatedUtc { get; set; }
}
