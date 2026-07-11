namespace TrStudio.Engine.Domain.Ops;

/// <summary>
/// A provider-neutral snapshot of the durable job queue and its worker servers (REQ-FN-064).
/// Produced by <see cref="IJobQueueMonitor"/> so the ops dashboard service can be unit-tested
/// without a live Hangfire storage.
/// </summary>
public sealed class JobQueueSnapshot
{
    /// <summary>The live worker servers processing the queue.</summary>
    public IReadOnlyList<JobServerInfo> Servers { get; init; } = Array.Empty<JobServerInfo>();

    /// <summary>Enqueued (awaiting a worker) depth per queue name.</summary>
    public IReadOnlyDictionary<string, long> EnqueuedByQueue { get; init; } = new Dictionary<string, long>();

    /// <summary>Fetched/processing depth per queue name.</summary>
    public IReadOnlyDictionary<string, long> ProcessingByQueue { get; init; } = new Dictionary<string, long>();
}

/// <summary>One worker server in a <see cref="JobQueueSnapshot"/>.</summary>
public sealed class JobServerInfo
{
    /// <summary>The server name/id.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The last heartbeat instant, if reported.</summary>
    public DateTime? Heartbeat { get; init; }

    /// <summary>Configured worker count on the server.</summary>
    public int WorkerCount { get; init; }

    /// <summary>The queues the server processes.</summary>
    public IReadOnlyList<string> Queues { get; init; } = Array.Empty<string>();
}
