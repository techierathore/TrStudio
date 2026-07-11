using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Ops;

namespace TrStudio.Engine.Infrastructure.Ops;

/// <summary>
/// Hangfire-backed <see cref="IJobQueueMonitor"/> reading the storage monitoring API (servers,
/// per-queue enqueued/fetched depth). Any monitoring failure (e.g. the storage not yet ready)
/// degrades to an empty snapshot rather than throwing so the ops dashboard stays available.
/// </summary>
public class HangfireJobQueueMonitor : IJobQueueMonitor
{
    private readonly JobStorage objJobStorage;
    private readonly ILogger<HangfireJobQueueMonitor> objLogger;

    /// <summary>
    /// Initialises the monitor.
    /// </summary>
    /// <param name="aJobStorage">The Hangfire job storage registered by the spine module.</param>
    /// <param name="aLogger">Structured logger.</param>
    public HangfireJobQueueMonitor(JobStorage aJobStorage, ILogger<HangfireJobQueueMonitor> aLogger)
    {
        objJobStorage = aJobStorage;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public JobQueueSnapshot GetSnapshot()
    {
        try
        {
            var vMonitoring = objJobStorage.GetMonitoringApi();

            var vServers = vMonitoring.Servers()
                .Select(aServer => new JobServerInfo
                {
                    Name = aServer.Name,
                    Heartbeat = aServer.Heartbeat,
                    WorkerCount = aServer.WorkersCount,
                    Queues = aServer.Queues?.ToList() ?? new List<string>()
                })
                .ToList();

            var vEnqueued = new Dictionary<string, long>(StringComparer.Ordinal);
            var vProcessing = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var vQueue in vMonitoring.Queues())
            {
                vEnqueued[vQueue.Name] = vQueue.Length;
                vProcessing[vQueue.Name] = vQueue.Fetched ?? 0L;
            }

            return new JobQueueSnapshot
            {
                Servers = vServers,
                EnqueuedByQueue = vEnqueued,
                ProcessingByQueue = vProcessing
            };
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "Reading the Hangfire queue snapshot failed; returning an empty snapshot.");
            return new JobQueueSnapshot();
        }
    }
}
