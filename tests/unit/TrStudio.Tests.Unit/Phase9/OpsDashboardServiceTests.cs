using NSubstitute;
using TrStudio.Engine.Domain.Ops;
using TrStudio.Models.Contracts;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Tests.Unit.Phase9;

/// <summary>
/// Unit tests for <see cref="OpsDashboardService"/> (REQ-FN-064/BRD-15): it fuses the live queue
/// snapshot, month-to-date spend and the generation-log error rate, computing queue totals, worker
/// health and the error-rate percentage.
/// </summary>
public class OpsDashboardServiceTests
{
    private readonly IJobQueueMonitor objMonitor = Substitute.For<IJobQueueMonitor>();
    private readonly ISpendLedger objSpendLedger = Substitute.For<ISpendLedger>();
    private readonly IGenerationLogRepository objLogRepository = Substitute.For<IGenerationLogRepository>();

    private OpsDashboardService CreateService() => new(objMonitor, objSpendLedger, objLogRepository);

    /// <summary>The dashboard totals the queue depth, reports a live worker as healthy and computes the error rate.</summary>
    [Fact]
    public async Task BuildsLiveDashboard()
    {
        objMonitor.GetSnapshot().Returns(new JobQueueSnapshot
        {
            Servers = new[]
            {
                new JobServerInfo { Name = "srv-1", Heartbeat = DateTime.UtcNow, WorkerCount = 2, Queues = new[] { "generation", "default" } }
            },
            EnqueuedByQueue = new Dictionary<string, long> { ["generation"] = 3, ["default"] = 1 },
            ProcessingByQueue = new Dictionary<string, long> { ["generation"] = 2 }
        });
        objSpendLedger.GetSummaryAsync(Arg.Any<CancellationToken>()).Returns(new SpendSummaryResponse { SpentToDateUsd = 12m });
        objLogRepository.GetOutcomeCountsSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int> { ["succeeded"] = 8, ["failed"] = 1, ["blocked"] = 1 });

        var vDashboard = await CreateService().GetDashboardAsync();

        Assert.True(vDashboard.Healthy);
        Assert.Equal(4, vDashboard.Queue.TotalEnqueued);
        Assert.Equal(2, vDashboard.Queue.TotalProcessing);
        Assert.Equal(12m, vDashboard.Spend.SpentToDateUsd);
        Assert.Equal(10, vDashboard.Errors.Total);
        Assert.Equal(20d, vDashboard.Errors.ErrorRatePct);
        Assert.Single(vDashboard.Workers);
    }

    /// <summary>A stale-only worker heartbeat marks the instance unhealthy; a zero-outcome window has a 0% error rate.</summary>
    [Fact]
    public async Task StaleHeartbeatIsUnhealthyAndEmptyWindowIsZeroErrorRate()
    {
        objMonitor.GetSnapshot().Returns(new JobQueueSnapshot
        {
            Servers = new[]
            {
                new JobServerInfo { Name = "srv-old", Heartbeat = DateTime.UtcNow.AddMinutes(-10), WorkerCount = 1 }
            }
        });
        objSpendLedger.GetSummaryAsync(Arg.Any<CancellationToken>()).Returns(new SpendSummaryResponse());
        objLogRepository.GetOutcomeCountsSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int>());

        var vDashboard = await CreateService().GetDashboardAsync();

        Assert.False(vDashboard.Healthy);
        Assert.Equal(0, vDashboard.Errors.Total);
        Assert.Equal(0d, vDashboard.Errors.ErrorRatePct);
    }
}
