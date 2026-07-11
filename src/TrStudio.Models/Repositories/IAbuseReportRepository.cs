using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for persisted abuse reports (SAFE-06 / BRD-65). Owner-scoped by the reporting
/// AppManager user id (BRD-81).
/// </summary>
public interface IAbuseReportRepository
{
    /// <summary>
    /// Persists an abuse report.
    /// </summary>
    /// <param name="aReport">The report to persist.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertAsync(AbuseReport aReport, CancellationToken aCancellationToken = default);
}
