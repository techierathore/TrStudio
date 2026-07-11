using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// A live progress update for a generation job, streamed to the UI over the SignalR
/// progress hub (<c>/hubs/progress</c>) while the job runs (BRD-5).
/// </summary>
public class JobProgressUpdate
{
    /// <summary>The job the update refers to.</summary>
    public Guid JobId { get; set; }

    /// <summary>The AppManager user id owning the job; used to route the update to that user's connections.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Current lifecycle state.</summary>
    public JobState State { get; set; }

    /// <summary>Progress percentage from 0 to 100.</summary>
    public int Progress { get; set; }

    /// <summary>Estimated time to completion, if the provider reports one.</summary>
    public TimeSpan? Eta { get; set; }

    /// <summary>Failure description when the job failed or was blocked.</summary>
    public string? FailureMessage { get; set; }

    /// <summary>Actionable remediation hint surfaced to the UI (e.g. "drop resolution to 256px").</summary>
    public string? RemediationHint { get; set; }
}
