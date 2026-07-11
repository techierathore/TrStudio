using TrStudio.Models.Enums;

namespace TrStudio.Models.Entities;

/// <summary>
/// A generation job — the unit of work submitted by a creator, cost-estimated and
/// safety-checked before it is enqueued on the durable Hangfire queue (Architecture §3).
/// </summary>
public class GenerationJob
{
    /// <summary>Unique job identifier.</summary>
    public Guid JobId { get; set; }

    /// <summary>The AppManager user id owning this job (per-user isolation, BRD-81).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The registry model id this job runs against.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>The generation task performed by this job.</summary>
    public GenerationTaskType Task { get; set; }

    /// <summary>The execution tier selected by the user for this job.</summary>
    public GenerationTier Tier { get; set; }

    /// <summary>The job parameters as a JSON document matching the model's param specs.</summary>
    public string ParamsJson { get; set; } = "{}";

    /// <summary>Current lifecycle state.</summary>
    public JobState State { get; set; }

    /// <summary>Progress percentage from 0 to 100.</summary>
    public int Progress { get; set; }

    /// <summary>Estimated time to completion reported by the provider, if available.</summary>
    public TimeSpan? Eta { get; set; }

    /// <summary>Pre-run cost estimate in the account currency, if the provider is billed.</summary>
    public decimal? CostEstimate { get; set; }

    /// <summary>Actual cost recorded after completion, if the provider is billed.</summary>
    public decimal? ActualCost { get; set; }

    /// <summary>Failure description when <see cref="State"/> is Failed or Blocked.</summary>
    public string? FailureMessage { get; set; }

    /// <summary>Actionable remediation hint surfaced to the UI (e.g. "drop to 256px or route to RunPod").</summary>
    public string? RemediationHint { get; set; }

    /// <summary>UTC timestamp when the job was created.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>UTC timestamp when the provider started executing, if started.</summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>UTC timestamp when the job reached a terminal state, if completed.</summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>The project this job belongs to, if any.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>The random seed actually used by the provider, for reproducibility.</summary>
    public long? SeedUsed { get; set; }
}
