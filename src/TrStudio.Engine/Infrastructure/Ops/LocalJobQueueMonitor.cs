using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Ops;
using TrStudio.Engine.Infrastructure.Jobs;

namespace TrStudio.Engine.Infrastructure.Ops;

/// <summary>
/// <see cref="IJobQueueMonitor"/> over the in-app runner and the local database (phase 3,
/// REQ-UI-103): one "server" (this app) with its workers, and the queued and running counts read
/// from <c>GenerationJob</c>. Replaces the Hangfire monitoring API.
/// </summary>
public class LocalJobQueueMonitor : IJobQueueMonitor
{
    /// <summary>The single queue name shown on the ops screen.</summary>
    public const string QueueName = "generation";

    private readonly InAppJobRunner objRunner;
    private readonly JobQueueStore objStore;
    private readonly ILogger<LocalJobQueueMonitor> objLogger;

    /// <summary>Creates the monitor.</summary>
    /// <param name="aRunner">The in-app runner.</param>
    /// <param name="aStore">The job queue store.</param>
    /// <param name="aLogger">Logger.</param>
    public LocalJobQueueMonitor(InAppJobRunner aRunner, JobQueueStore aStore, ILogger<LocalJobQueueMonitor> aLogger)
    {
        objRunner = aRunner;
        objStore = aStore;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public JobQueueSnapshot GetSnapshot()
    {
        try
        {
            var (vQueued, vRunning) = objStore.CountQueuedAndRunning();
            var vServers = objRunner.StartedUtc is null
                ? Array.Empty<JobServerInfo>()
                : new[]
                {
                    new JobServerInfo
                    {
                        Name = "TrStudio (in this app)",
                        Heartbeat = DateTime.UtcNow,
                        WorkerCount = objRunner.WorkerCount,
                        Queues = new[] { QueueName }
                    }
                };

            return new JobQueueSnapshot
            {
                Servers = vServers,
                EnqueuedByQueue = new Dictionary<string, long> { [QueueName] = vQueued },
                ProcessingByQueue = new Dictionary<string, long> { [QueueName] = vRunning }
            };
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "Reading the local job queue failed; returning an empty snapshot.");
            return new JobQueueSnapshot();
        }
    }
}
