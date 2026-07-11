using TrStudio.Models.Enums;

namespace TrStudio.Models.Entities;

/// <summary>
/// A persisted spend entry written once per completed billed job, feeding the month-to-date
/// spend total and the monthly-cap guardrail (REQ-FN-029/BRD-13, NFR-007). Local/free jobs are
/// not recorded.
/// </summary>
public class SpendRecord
{
    /// <summary>Unique spend-record identifier.</summary>
    public Guid SpendRecordId { get; set; }

    /// <summary>The AppManager user id that owned the job (audit only; the cap is instance-global).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The generation job this spend was incurred by.</summary>
    public Guid JobId { get; set; }

    /// <summary>The registry model the job ran against.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>The billing channel this spend accrued against.</summary>
    public SpendChannel Channel { get; set; }

    /// <summary>The actual cost incurred, in the account currency (USD).</summary>
    public decimal AmountUsd { get; set; }

    /// <summary>UTC timestamp when the spend was recorded.</summary>
    public DateTime CreatedUtc { get; set; }
}
