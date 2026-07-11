namespace TrStudio.Models.Contracts;

/// <summary>
/// The result of filing an abuse report (SAFE-06 / BRD-65).
/// </summary>
public class AbuseReportResponse
{
    /// <summary>The persisted report id.</summary>
    public Guid ReportId { get; set; }

    /// <summary>The triage status the report was created with.</summary>
    public string Status { get; set; } = "open";

    /// <summary>A human-readable confirmation message.</summary>
    public string Message { get; set; } = string.Empty;
}
