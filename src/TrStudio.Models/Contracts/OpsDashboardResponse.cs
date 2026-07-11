namespace TrStudio.Models.Contracts;

/// <summary>
/// Live operations snapshot for the admin ops dashboard (REQ-FN-064/BRD-15): worker health,
/// Hangfire queue depth, month-to-date spend and the recent error rate — the data source behind
/// the already-built admin ops page (UI-073).
/// </summary>
public class OpsDashboardResponse
{
    /// <summary>UTC instant this snapshot was generated.</summary>
    public DateTime GeneratedUtc { get; set; }

    /// <summary>Whether at least one worker heartbeat is recent (the queue is being drained).</summary>
    public bool Healthy { get; set; }

    /// <summary>The live Hangfire worker servers.</summary>
    public List<OpsWorker> Workers { get; set; } = new();

    /// <summary>The live queue-depth snapshot.</summary>
    public OpsQueue Queue { get; set; } = new();

    /// <summary>Month-to-date spend against the instance cap (reused from the spend ledger).</summary>
    public SpendSummaryResponse Spend { get; set; } = new();

    /// <summary>The recent-window error rate computed from the generation log outcomes.</summary>
    public OpsErrorRate Errors { get; set; } = new();
}

/// <summary>One live Hangfire worker server.</summary>
public class OpsWorker
{
    /// <summary>The server name/id reported by Hangfire.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Last heartbeat instant, if reported.</summary>
    public DateTime? Heartbeat { get; set; }

    /// <summary>Configured worker count on the server.</summary>
    public int WorkerCount { get; set; }

    /// <summary>The queues the server processes.</summary>
    public List<string> Queues { get; set; } = new();
}

/// <summary>The Hangfire queue-depth snapshot.</summary>
public class OpsQueue
{
    /// <summary>Total jobs enqueued and awaiting a worker across all queues.</summary>
    public long TotalEnqueued { get; set; }

    /// <summary>Total jobs currently fetched/processing across all queues.</summary>
    public long TotalProcessing { get; set; }

    /// <summary>Enqueued depth per queue name.</summary>
    public Dictionary<string, long> EnqueuedByQueue { get; set; } = new();
}

/// <summary>The recent error rate over a rolling window, from the generation log.</summary>
public class OpsErrorRate
{
    /// <summary>The rolling window size in hours the counts cover.</summary>
    public int WindowHours { get; set; }

    /// <summary>Total generation outcomes recorded in the window.</summary>
    public int Total { get; set; }

    /// <summary>Successful outcomes in the window.</summary>
    public int Succeeded { get; set; }

    /// <summary>Failed outcomes in the window.</summary>
    public int Failed { get; set; }

    /// <summary>Safety-blocked outcomes in the window.</summary>
    public int Blocked { get; set; }

    /// <summary>The error rate as a percentage of total outcomes (failed + blocked).</summary>
    public double ErrorRatePct { get; set; }
}
