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

    /// <summary>
    /// Where <see cref="ActualCost"/> came from when a provider states its own price:
    /// <c>"venice-quote"</c> (video quote taken before queueing) or <c>"venice-list"</c> (the listed
    /// picture price read at call time); null for estimate-priced or free runs (owner decision 1A).
    /// </summary>
    public string? PriceSource { get; set; }

    /// <summary>
    /// The provider kind of the registry row the job ran on (read-only, joined from the registry on
    /// read; e.g. <c>"Venice"</c>, <c>"ComfyUi"</c>); null when the row has since been deleted.
    /// </summary>
    public string? ProviderKind { get; set; }

    /// <summary>UTC timestamp when the entry was written.</summary>
    public DateTime CreatedUtc { get; set; }
}
