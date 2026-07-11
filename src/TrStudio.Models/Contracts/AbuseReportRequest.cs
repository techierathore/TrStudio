namespace TrStudio.Models.Contracts;

/// <summary>
/// Files an abuse report against content or a generation (SAFE-06 / BRD-65).
/// </summary>
public class AbuseReportRequest
{
    /// <summary>What is being reported (e.g. <c>asset</c>, <c>job</c>, <c>character</c>).</summary>
    public string TargetKind { get; set; } = string.Empty;

    /// <summary>A reference identifying the reported target (an id or descriptor).</summary>
    public string TargetReference { get; set; } = string.Empty;

    /// <summary>The abuse category (e.g. <c>csam</c>, <c>ncii</c>, <c>impersonation</c>, <c>other</c>).</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Free-text details supplied by the reporter.</summary>
    public string Details { get; set; } = string.Empty;
}
