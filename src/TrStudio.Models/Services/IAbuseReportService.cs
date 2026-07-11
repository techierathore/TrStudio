using TrStudio.Models.Contracts;

namespace TrStudio.Models.Services;

/// <summary>
/// Files and persists abuse reports (SAFE-06 / BRD-65). Reports are retained for the deployment
/// operator's review; owner-scoped by the reporting AppManager user id (BRD-81).
/// </summary>
public interface IAbuseReportService
{
    /// <summary>
    /// Files an abuse report and persists it.
    /// </summary>
    /// <param name="aRequest">The report to file.</param>
    /// <param name="aUserId">The reporting AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The persisted report result.</returns>
    Task<AbuseReportResponse> FileAsync(AbuseReportRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);
}
