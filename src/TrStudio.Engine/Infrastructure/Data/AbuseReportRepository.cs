using Dapper;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IAbuseReportRepository"/> over the <c>"AbuseReport"</c> table (script
/// 0900, SAFE-06). Rows are owner-scoped by the reporting AppManager user id (BRD-81); identifiers
/// are quoted PascalCase.
/// </summary>
public class AbuseReportRepository : IAbuseReportRepository
{
    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public AbuseReportRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(AbuseReport aReport, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            INSERT INTO "AbuseReport"
                ("ReportId", "ReporterUserId", "TargetKind", "TargetReference", "Category", "Details", "Status", "CreatedUtc")
            VALUES (@aReportId, @aReporterUserId, @aTargetKind, @aTargetReference, @aCategory, @aDetails, @aStatus, @aCreatedUtc)
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(Sql, new
        {
            aReportId = aReport.ReportId,
            aReporterUserId = aReport.ReporterUserId,
            aTargetKind = aReport.TargetKind,
            aTargetReference = aReport.TargetReference,
            aCategory = aReport.Category,
            aDetails = aReport.Details,
            aStatus = aReport.Status,
            aCreatedUtc = aReport.CreatedUtc
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }
}
