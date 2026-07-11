using Microsoft.Extensions.Logging;
using TrStudio.Models.Contracts;

namespace TrStudio.Engine.Domain.Progress;

/// <summary>
/// In-process <see cref="IProgressNotifier"/> fan-out. Subscribers (the API's SignalR bridge,
/// tests) register a handler; the job worker publishes updates through the same singleton.
/// A faulting subscriber is logged and never breaks the publishing job or other subscribers.
/// </summary>
public class ProgressBroadcaster : IProgressNotifier
{
    private readonly List<Func<JobProgressUpdate, Task>> objSubscribers = new();
    private readonly object objSubscriberLock = new();
    private readonly ILogger<ProgressBroadcaster> objLogger;

    /// <summary>
    /// Initialises the broadcaster.
    /// </summary>
    /// <param name="aLogger">Logger for subscriber failures.</param>
    public ProgressBroadcaster(ILogger<ProgressBroadcaster> aLogger)
    {
        objLogger = aLogger;
    }

    /// <summary>
    /// Registers a handler invoked for every published progress update.
    /// </summary>
    /// <param name="aHandler">The asynchronous handler to invoke.</param>
    public void Subscribe(Func<JobProgressUpdate, Task> aHandler)
    {
        lock (objSubscriberLock)
        {
            objSubscribers.Add(aHandler);
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync(JobProgressUpdate aUpdate, CancellationToken aCancellationToken = default)
    {
        Func<JobProgressUpdate, Task>[] vHandlers;
        lock (objSubscriberLock)
        {
            vHandlers = objSubscribers.ToArray();
        }

        foreach (var vHandler in vHandlers)
        {
            try
            {
                await vHandler(aUpdate).ConfigureAwait(false);
            }
            catch (Exception vException)
            {
                objLogger.LogWarning(vException, "Progress subscriber failed for job {JobId}", aUpdate.JobId);
            }
        }
    }
}
