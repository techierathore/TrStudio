using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// Response body for a job status query (<c>GET /api/v1/jobs/{id}</c>), also pushed
/// over the SignalR progress hub while the job runs.
/// </summary>
public class JobStatusResponse
{
    /// <summary>The job identifier.</summary>
    public Guid JobId { get; set; }

    /// <summary>Current lifecycle state.</summary>
    public JobState State { get; set; }

    /// <summary>Progress percentage from 0 to 100.</summary>
    public int Progress { get; set; }

    /// <summary>Estimated time to completion, if the provider reports one.</summary>
    public TimeSpan? Eta { get; set; }

    /// <summary>Failure description when the job failed or was blocked.</summary>
    public string? FailureMessage { get; set; }

    /// <summary>Actionable remediation hint surfaced to the UI.</summary>
    public string? RemediationHint { get; set; }

    /// <summary>Pre-run cost estimate, if the provider is billed.</summary>
    public decimal? CostEstimate { get; set; }

    /// <summary>Actual recorded cost after completion, if the provider is billed.</summary>
    public decimal? ActualCost { get; set; }
}
