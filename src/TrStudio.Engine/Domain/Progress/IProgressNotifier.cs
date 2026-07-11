using TrStudio.Models.Contracts;

namespace TrStudio.Engine.Domain.Progress;

/// <summary>
/// Engine-side seam for publishing live job progress (REQ-FN-005). The job worker publishes
/// through this abstraction; the API host bridges it onto the SignalR progress hub so the
/// engine never references SignalR directly.
/// </summary>
public interface IProgressNotifier
{
    /// <summary>
    /// Publishes a progress update for a running job.
    /// </summary>
    /// <param name="aUpdate">The update carrying state, progress, ETA and any failure details.</param>
    /// <param name="aCancellationToken">Token to cancel the publish.</param>
    /// <returns>A task completing when all subscribers have been notified.</returns>
    Task PublishAsync(JobProgressUpdate aUpdate, CancellationToken aCancellationToken = default);
}
