using Dapper;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="ISafetyAuditRepository"/> over the append-only <c>"SafetyAuditEntry"</c>
/// table (script 0900, SAFE-06). Enum columns are stored as text; identifiers are quoted PascalCase.
/// The list read is the admin-scoped, deployment-wide audit trail.
/// </summary>
public class SafetyAuditRepository : ISafetyAuditRepository
{
    private const string SelectColumns =
        "\"AuditId\", \"JobId\", \"UserId\", \"ModelId\", \"Phase\", \"Allowed\", \"Floor\", \"Reason\", \"Detail\", \"CreatedUtc\"";

    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public SafetyAuditRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(SafetyAuditEntry aEntry, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            INSERT INTO "SafetyAuditEntry"
                ("AuditId", "JobId", "UserId", "ModelId", "Phase", "Allowed", "Floor", "Reason", "Detail", "CreatedUtc")
            VALUES (@aAuditId, @aJobId, @aUserId, @aModelId, @aPhase, @aAllowed, @aFloor, @aReason, @aDetail, @aCreatedUtc)
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(Sql, new
        {
            aAuditId = aEntry.AuditId,
            aJobId = aEntry.JobId,
            aUserId = aEntry.UserId,
            aModelId = aEntry.ModelId,
            aPhase = aEntry.Phase.ToString(),
            aAllowed = aEntry.Allowed,
            aFloor = aEntry.Floor.ToString(),
            aReason = aEntry.Reason,
            aDetail = aEntry.Detail,
            aCreatedUtc = aEntry.CreatedUtc
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PagedResult<SafetyAuditEntry>> ListAsync(int aPage, int aPageSize, CancellationToken aCancellationToken = default)
    {
        var vPage = Math.Max(1, aPage);
        var vPageSize = Math.Clamp(aPageSize, 1, 200);
        var vSql =
            $"SELECT {SelectColumns} FROM \"SafetyAuditEntry\" ORDER BY \"CreatedUtc\" DESC LIMIT @aLimit OFFSET @aOffset; " +
            "SELECT COUNT(*) FROM \"SafetyAuditEntry\"";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        using var vMulti = await vConnection.QueryMultipleAsync(new CommandDefinition(
            vSql,
            new { aLimit = vPageSize, aOffset = (vPage - 1) * vPageSize },
            cancellationToken: aCancellationToken)).ConfigureAwait(false);

        var vRows = (await vMulti.ReadAsync<AuditRow>().ConfigureAwait(false)).Select(aRow => aRow.ToEntity()).ToList();
        var vTotal = await vMulti.ReadSingleAsync<int>().ConfigureAwait(false);
        return new PagedResult<SafetyAuditEntry> { Items = vRows, TotalCount = vTotal, Page = vPage, PageSize = vPageSize };
    }

    /// <summary>Flat Dapper row parsing the text enum columns back to their strongly-typed values.</summary>
    private sealed class AuditRow
    {
        public Guid AuditId { get; set; }
        public Guid? JobId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty;
        public bool Allowed { get; set; }
        public string Floor { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }

        public SafetyAuditEntry ToEntity() => new()
        {
            AuditId = AuditId,
            JobId = JobId,
            UserId = UserId,
            ModelId = ModelId,
            Phase = Enum.Parse<SafetyPhase>(Phase),
            Allowed = Allowed,
            Floor = Enum.Parse<SafetyFloor>(Floor),
            Reason = Reason,
            Detail = Detail,
            CreatedUtc = CreatedUtc
        };
    }
}
