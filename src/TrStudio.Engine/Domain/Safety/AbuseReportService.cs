using Microsoft.Extensions.Logging;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Safety;

/// <summary>
/// Default <see cref="IAbuseReportService"/> (SAFE-06 / BRD-65). Files and persists abuse reports
/// for the deployment operator's review; owner-scoped by the reporting AppManager user id (BRD-81).
/// </summary>
public class AbuseReportService : IAbuseReportService
{
    private readonly IAbuseReportRepository objRepository;
    private readonly ILogger<AbuseReportService> objLogger;

    /// <summary>
    /// Initialises the service.
    /// </summary>
    /// <param name="aRepository">Abuse-report persistence.</param>
    /// <param name="aLogger">Structured logger.</param>
    public AbuseReportService(IAbuseReportRepository aRepository, ILogger<AbuseReportService> aLogger)
    {
        objRepository = aRepository;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<AbuseReportResponse> FileAsync(AbuseReportRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vReport = new AbuseReport
        {
            ReportId = Guid.NewGuid(),
            ReporterUserId = aUserId,
            TargetKind = string.IsNullOrWhiteSpace(aRequest.TargetKind) ? "unspecified" : aRequest.TargetKind.Trim(),
            TargetReference = aRequest.TargetReference?.Trim() ?? string.Empty,
            Category = string.IsNullOrWhiteSpace(aRequest.Category) ? "other" : aRequest.Category.Trim(),
            Details = aRequest.Details ?? string.Empty,
            Status = "open",
            CreatedUtc = DateTime.UtcNow
        };

        await objRepository.InsertAsync(vReport, aCancellationToken).ConfigureAwait(false);
        objLogger.LogInformation("Abuse report {ReportId} filed ({Category}, target {TargetKind})", vReport.ReportId, vReport.Category, vReport.TargetKind);

        return new AbuseReportResponse
        {
            ReportId = vReport.ReportId,
            Status = vReport.Status,
            Message = "Abuse report received and retained for review (SAFE-06)."
        };
    }
}
