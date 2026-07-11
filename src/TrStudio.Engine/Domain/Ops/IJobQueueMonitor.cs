namespace TrStudio.Engine.Domain.Ops;

/// <summary>
/// Reads live queue-depth and worker-health from the durable job queue (REQ-FN-064). Abstracted
/// from Hangfire so the ops dashboard and observability gauges depend on a testable seam rather
/// than the storage monitoring API directly.
/// </summary>
public interface IJobQueueMonitor
{
    /// <summary>
    /// Captures a point-in-time snapshot of queue depth and worker servers.
    /// </summary>
    /// <returns>The current snapshot; an empty snapshot when the storage is not yet available.</returns>
    JobQueueSnapshot GetSnapshot();
}
