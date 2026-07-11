using Dapper;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IGenerationLogRepository"/> over the append-only
/// <c>"GenerationLog"</c> table (audit trail, SAFE-06). Every read applies the
/// <c>"UserId"</c> owner filter server-side (BRD-81).
/// </summary>
public class GenerationLogRepository : IGenerationLogRepository
{
    private const string SelectColumns =
        "\"GenerationLogId\" AS \"LogId\", \"GenerationJobId\" AS \"JobId\", \"UserId\", \"ModelId\", " +
        "\"Tier\", \"ParamsJson\", \"CostEstimate\", \"ActualCost\", \"DurationMs\", \"Outcome\", " +
        "\"FailureMessage\", \"CreatedUtc\"";

    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public GenerationLogRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(GenerationLogEntry aEntry, CancellationToken aCancellationToken = default)
    {
        const string vSql =
            "INSERT INTO \"GenerationLog\" (\"GenerationLogId\", \"GenerationJobId\", \"UserId\", \"ModelId\", " +
            "\"Tier\", \"ParamsJson\", \"CostEstimate\", \"ActualCost\", \"DurationMs\", \"Outcome\", " +
            "\"FailureMessage\", \"CreatedUtc\") " +
            "VALUES (@LogId, @JobId, @UserId, @ModelId, @Tier, CAST(@ParamsJson AS jsonb), @CostEstimate, " +
            "@ActualCost, @DurationMs, @Outcome, @FailureMessage, @CreatedUtc)";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(vSql, new
        {
            aEntry.LogId,
            aEntry.JobId,
            aEntry.UserId,
            aEntry.ModelId,
            Tier = aEntry.Tier.ToString(),
            aEntry.ParamsJson,
            aEntry.CostEstimate,
            aEntry.ActualCost,
            aEntry.DurationMs,
            aEntry.Outcome,
            aEntry.FailureMessage,
            aEntry.CreatedUtc
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GenerationLogEntry?> GetAsync(Guid aLogId, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vSql = $"SELECT {SelectColumns} FROM \"GenerationLog\" WHERE \"GenerationLogId\" = @aLogId AND \"UserId\" = @aUserId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.QuerySingleOrDefaultAsync<GenerationLogEntry>(
            new CommandDefinition(vSql, new { aLogId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PagedResult<GenerationLogEntry>> ListAsync(string aUserId, int aPage, int aPageSize, CancellationToken aCancellationToken = default)
    {
        var vPage = Math.Max(1, aPage);
        var vPageSize = Math.Clamp(aPageSize, 1, 200);
        var vSql =
            $"SELECT {SelectColumns} FROM \"GenerationLog\" WHERE \"UserId\" = @aUserId " +
            "ORDER BY \"CreatedUtc\" DESC LIMIT @aLimit OFFSET @aOffset; " +
            "SELECT COUNT(*) FROM \"GenerationLog\" WHERE \"UserId\" = @aUserId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        using var vMulti = await vConnection.QueryMultipleAsync(new CommandDefinition(
            vSql,
            new { aUserId, aLimit = vPageSize, aOffset = (vPage - 1) * vPageSize },
            cancellationToken: aCancellationToken)).ConfigureAwait(false);

        var vItems = (await vMulti.ReadAsync<GenerationLogEntry>().ConfigureAwait(false)).ToList();
        var vTotal = await vMulti.ReadSingleAsync<int>().ConfigureAwait(false);
        return new PagedResult<GenerationLogEntry> { Items = vItems, TotalCount = vTotal, Page = vPage, PageSize = vPageSize };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, int>> GetOutcomeCountsSinceAsync(DateTime aSinceUtc, CancellationToken aCancellationToken = default)
    {
        // Instance-global aggregate for the admin ops dashboard (REQ-FN-064) — deliberately NOT
        // owner-filtered; only ever reached behind the AdminOnly policy.
        const string vSql =
            "SELECT \"Outcome\", COUNT(*) AS \"Count\" FROM \"GenerationLog\" " +
            "WHERE \"CreatedUtc\" >= @aSinceUtc GROUP BY \"Outcome\"";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRows = await vConnection.QueryAsync<OutcomeCountRow>(
            new CommandDefinition(vSql, new { aSinceUtc }, cancellationToken: aCancellationToken)).ConfigureAwait(false);

        var vResult = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var vRow in vRows)
        {
            vResult[vRow.Outcome ?? string.Empty] = vRow.Count;
        }

        return vResult;
    }

    /// <summary>Flat Dapper row for the per-outcome generation-log aggregate.</summary>
    private sealed class OutcomeCountRow
    {
        public string? Outcome { get; set; }
        public int Count { get; set; }
    }
}
