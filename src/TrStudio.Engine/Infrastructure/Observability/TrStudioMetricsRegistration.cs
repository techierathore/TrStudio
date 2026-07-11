using System.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Ops;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Infrastructure.Observability;

/// <summary>
/// Registers the observable gauges that expose live queue depth and month-to-date spend as
/// OpenTelemetry metrics (NFR-008/BRD-76). Queue depth is read synchronously from the job-queue
/// monitor on each collection; spend is refreshed on a background timer (a gauge callback must stay
/// non-blocking) and the last value is published. Runs as a hosted service so the gauges exist for
/// the lifetime of the host.
/// </summary>
public sealed class TrStudioMetricsRegistration : IHostedService, IDisposable
{
    private static readonly TimeSpan SpendRefreshInterval = TimeSpan.FromSeconds(30);

    private readonly IJobQueueMonitor objQueueMonitor;
    private readonly ISpendLedger objSpendLedger;
    private readonly ILogger<TrStudioMetricsRegistration> objLogger;

    private double objLastSpendUsd;
    private Timer? objTimer;

    /// <summary>
    /// Initialises the registration and wires the queue-depth observable gauges onto the shared meter.
    /// </summary>
    /// <param name="aQueueMonitor">Live queue snapshot source (synchronous, gauge-safe).</param>
    /// <param name="aSpendLedger">Spend summary source, sampled on a timer.</param>
    /// <param name="aLogger">Structured logger.</param>
    public TrStudioMetricsRegistration(
        IJobQueueMonitor aQueueMonitor,
        ISpendLedger aSpendLedger,
        ILogger<TrStudioMetricsRegistration> aLogger)
    {
        objQueueMonitor = aQueueMonitor;
        objSpendLedger = aSpendLedger;
        objLogger = aLogger;

        TrStudioTelemetry.Meter.CreateObservableGauge(
            "trstudio.queue.enqueued",
            ObserveEnqueued,
            "jobs",
            "Jobs enqueued and awaiting a worker across all queues.");

        TrStudioTelemetry.Meter.CreateObservableGauge(
            "trstudio.queue.processing",
            ObserveProcessing,
            "jobs",
            "Jobs currently fetched/processing across all queues.");

        TrStudioTelemetry.Meter.CreateObservableGauge(
            "trstudio.spend.month_to_date_usd",
            () => objLastSpendUsd,
            "usd",
            "Month-to-date spend across all billed channels (sampled).");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken aCancellationToken)
    {
        objTimer = new Timer(_ => RefreshSpend(), null, TimeSpan.Zero, SpendRefreshInterval);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken aCancellationToken)
    {
        objTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose() => objTimer?.Dispose();

    private Measurement<long> ObserveEnqueued()
        => new(SafeSum(aSnapshot => aSnapshot.EnqueuedByQueue));

    private Measurement<long> ObserveProcessing()
        => new(SafeSum(aSnapshot => aSnapshot.ProcessingByQueue));

    private long SafeSum(Func<JobQueueSnapshot, IReadOnlyDictionary<string, long>> aSelector)
    {
        try
        {
            return aSelector(objQueueMonitor.GetSnapshot()).Values.Sum();
        }
        catch (Exception vException)
        {
            objLogger.LogDebug(vException, "Queue-depth gauge sampling failed; reporting 0.");
            return 0L;
        }
    }

    private void RefreshSpend()
    {
        try
        {
            var vSummary = objSpendLedger.GetSummaryAsync().GetAwaiter().GetResult();
            objLastSpendUsd = (double)vSummary.SpentToDateUsd;
        }
        catch (Exception vException)
        {
            objLogger.LogDebug(vException, "Spend gauge refresh failed; keeping the last sampled value.");
        }
    }
}
