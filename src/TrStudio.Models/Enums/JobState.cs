namespace TrStudio.Models.Enums;

/// <summary>
/// Lifecycle state of a generation job in the durable Hangfire queue.
/// </summary>
public enum JobState
{
    /// <summary>Created and cost-estimated but not yet enqueued.</summary>
    Pending,

    /// <summary>Enqueued and waiting for a worker.</summary>
    Queued,

    /// <summary>Currently executing on a provider.</summary>
    Running,

    /// <summary>Completed successfully; assets are available.</summary>
    Succeeded,

    /// <summary>Completed with an error; see the failure message and remediation hint.</summary>
    Failed,

    /// <summary>Cancelled by the user before completion.</summary>
    Cancelled,

    /// <summary>Rejected by the non-disableable safety pipeline (SAFE-01..06).</summary>
    Blocked
}
