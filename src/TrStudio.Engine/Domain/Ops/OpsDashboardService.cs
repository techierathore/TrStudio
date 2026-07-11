using TrStudio.Models.Contracts;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Ops;

/// <summary>
/// Default <see cref="IOpsDashboardService"/> (REQ-FN-064/BRD-15). Fuses the live queue/worker
/// snapshot, the month-to-date spend summary and a rolling-window error rate over the generation
/// log into the single payload the admin ops page renders.
/// </summary>
public class OpsDashboardService : IOpsDashboardService
{
    /// <summary>Heartbeats newer than this are considered "healthy" (worker is alive and draining).</summary>
    private static readonly TimeSpan HealthyHeartbeat = TimeSpan.FromSeconds(60);

    /// <summary>The rolling window the error rate is computed over.</summary>
    private const int ErrorWindowHours = 24;

    private readonly IJobQueueMonitor objQueueMonitor;
    private readonly ISpendLedger objSpendLedger;
    private readonly IGenerationLogRepository objLogRepository;

    /// <summary>
    /// Initialises the service.
    /// </summary>
    /// <param name="aQueueMonitor">Live queue/worker snapshot source.</param>
    /// <param name="aSpendLedger">Month-to-date spend summary source.</param>
    /// <param name="aLogRepository">Generation-log outcome-count source for the error rate.</param>
    public OpsDashboardService(
        IJobQueueMonitor aQueueMonitor,
        ISpendLedger aSpendLedger,
        IGenerationLogRepository aLogRepository)
    {
        objQueueMonitor = aQueueMonitor;
        objSpendLedger = aSpendLedger;
        objLogRepository = aLogRepository;
    }

    /// <inheritdoc />
    public async Task<OpsDashboardResponse> GetDashboardAsync(CancellationToken aCancellationToken = default)
    {
        var vNow = DateTime.UtcNow;
        var vSnapshot = objQueueMonitor.GetSnapshot();
        var vSpend = await objSpendLedger.GetSummaryAsync(aCancellationToken).ConfigureAwait(false);
        var vOutcomes = await objLogRepository
            .GetOutcomeCountsSinceAsync(vNow.AddHours(-ErrorWindowHours), aCancellationToken)
            .ConfigureAwait(false);

        var vWorkers = vSnapshot.Servers
            .Select(aServer => new OpsWorker
            {
                Name = aServer.Name,
                Heartbeat = aServer.Heartbeat,
                WorkerCount = aServer.WorkerCount,
                Queues = aServer.Queues.ToList()
            })
            .ToList();

        var vHealthy = vSnapshot.Servers.Any(aServer =>
            aServer.Heartbeat is { } vBeat && vNow - vBeat <= HealthyHeartbeat);

        var vQueue = new OpsQueue
        {
            EnqueuedByQueue = vSnapshot.EnqueuedByQueue.ToDictionary(aPair => aPair.Key, aPair => aPair.Value),
            TotalEnqueued = vSnapshot.EnqueuedByQueue.Values.Sum(),
            TotalProcessing = vSnapshot.ProcessingByQueue.Values.Sum()
        };

        return new OpsDashboardResponse
        {
            GeneratedUtc = vNow,
            Healthy = vHealthy,
            Workers = vWorkers,
            Queue = vQueue,
            Spend = vSpend,
            Errors = BuildErrorRate(vOutcomes)
        };
    }

    private static OpsErrorRate BuildErrorRate(IReadOnlyDictionary<string, int> aOutcomes)
    {
        var vSucceeded = Count(aOutcomes, "succeeded");
        var vFailed = Count(aOutcomes, "failed");
        var vBlocked = Count(aOutcomes, "blocked");
        var vTotal = aOutcomes.Values.Sum();
        var vErrorCount = vFailed + vBlocked;

        return new OpsErrorRate
        {
            WindowHours = ErrorWindowHours,
            Total = vTotal,
            Succeeded = vSucceeded,
            Failed = vFailed,
            Blocked = vBlocked,
            ErrorRatePct = vTotal == 0 ? 0d : Math.Round(vErrorCount * 100d / vTotal, 2)
        };
    }

    private static int Count(IReadOnlyDictionary<string, int> aOutcomes, string aOutcome)
        => aOutcomes.TryGetValue(aOutcome, out var vCount) ? vCount : 0;
}
