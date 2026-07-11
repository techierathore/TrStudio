using TrStudio.Models.Enums;

namespace TrStudio.Models.Entities;

/// <summary>
/// An immutable generation-log row recording what ran, at what cost and with what outcome —
/// part of the audit trail (SAFE-06) and the spend dashboard's data source.
/// </summary>
public class GenerationLogEntry
{
    /// <summary>Unique log entry identifier.</summary>
    public Guid LogId { get; set; }

    /// <summary>The job this entry records.</summary>
    public Guid JobId { get; set; }

    /// <summary>The AppManager user id that ran the job (per-user isolation, BRD-81).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The registry model id the job ran against.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>The execution tier the job ran on.</summary>
    public GenerationTier Tier { get; set; }

    /// <summary>The job parameters JSON snapshot.</summary>
    public string ParamsJson { get; set; } = "{}";

    /// <summary>Pre-run cost estimate, if the provider is billed.</summary>
    public decimal? CostEstimate { get; set; }

    /// <summary>Actual recorded cost, if the provider is billed.</summary>
    public decimal? ActualCost { get; set; }

    /// <summary>Wall-clock duration of the job in milliseconds, if completed.</summary>
    public long? DurationMs { get; set; }

    /// <summary>The outcome label (e.g. "succeeded", "failed", "cancelled", "blocked").</summary>
    public string Outcome { get; set; } = string.Empty;

    /// <summary>Failure description for unsuccessful outcomes.</summary>
    public string? FailureMessage { get; set; }

    /// <summary>UTC timestamp when the entry was written.</summary>
    public DateTime CreatedUtc { get; set; }
}
