using TrStudio.Models.Contracts;

namespace TrStudio.Engine.Domain.Ops;

/// <summary>
/// Assembles the live admin operations snapshot (REQ-FN-064/BRD-15): worker health + queue depth
/// (from <see cref="IJobQueueMonitor"/>), month-to-date spend (from the spend ledger) and the
/// recent error rate (from the generation log).
/// </summary>
public interface IOpsDashboardService
{
    /// <summary>
    /// Builds the current ops-dashboard snapshot.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The live operations snapshot.</returns>
    Task<OpsDashboardResponse> GetDashboardAsync(CancellationToken aCancellationToken = default);
}
