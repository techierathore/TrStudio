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
    /// <summary>
    /// The log columns plus the registry row's provider kind (LEFT JOIN, so a deleted row reads null).
    /// Aliased <c>l</c> / <c>r</c>; every query below selects FROM <c>"GenerationLog" l</c>.
    /// </summary>
    internal const string SelectColumns =
        "l.\"GenerationLogId\" AS \"LogId\", l.\"GenerationJobId\" AS \"JobId\", l.\"UserId\", l.\"ModelId\", " +
        "l.\"Tier\", l.\"ParamsJson\", l.\"CostEstimate\", l.\"ActualCost\", l.\"DurationMs\", l.\"Outcome\", " +
        "l.\"FailureMessage\", l.\"CreatedUtc\", l.\"PriceSource\", r.\"ProviderKind\"";

    /// <summary>The FROM clause pairing each log row with its registry row for the provider kind.</summary>
    internal const string FromClause =
        "FROM \"GenerationLog\" l LEFT JOIN \"RegistryModel\" r ON r.\"RegistryModelId\" = l.\"ModelId\"";

    /// <summary>The append-only insert; <c>"PriceSource"</c> records where <c>"ActualCost"</c> came from (REQ-FN-082).</summary>
    internal const string InsertSql =
        "INSERT INTO \"GenerationLog\" (\"GenerationLogId\", \"GenerationJobId\", \"UserId\", \"ModelId\", " +
        "\"Tier\", \"ParamsJson\", \"CostEstimate\", \"ActualCost\", \"DurationMs\", \"Outcome\", " +
        "\"FailureMessage\", \"PriceSource\", \"CreatedUtc\") " +
        "VALUES (@LogId, @JobId, @UserId, @ModelId, @Tier, @ParamsJson, @CostEstimate, " +
        "@ActualCost, @DurationMs, @Outcome, @FailureMessage, @PriceSource, @CreatedUtc)";

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
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(InsertSql, new
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
            aEntry.PriceSource,
            aEntry.CreatedUtc
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GenerationLogEntry?> GetAsync(Guid aLogId, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vSql = $"SELECT {SelectColumns} {FromClause} WHERE l.\"GenerationLogId\" = @aLogId AND l.\"UserId\" = @aUserId";

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
            $"SELECT {SelectColumns} {FromClause} WHERE l.\"UserId\" = @aUserId " +
            "ORDER BY l.\"CreatedUtc\" DESC LIMIT @aLimit OFFSET @aOffset; " +
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
    public async Task<IReadOnlyList<GenerationLogEntry>> ListByJobsAsync(IReadOnlyCollection<Guid> aJobIds, string aUserId, CancellationToken aCancellationToken = default)
    {
        if (aJobIds.Count == 0)
        {
            return Array.Empty<GenerationLogEntry>();
        }

        var vSql =
            $"SELECT {SelectColumns} {FromClause} " +
            "WHERE l.\"UserId\" = @aUserId AND l.\"GenerationJobId\" IN @aJobIds ORDER BY l.\"CreatedUtc\"";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRows = await vConnection.QueryAsync<GenerationLogEntry>(new CommandDefinition(
            vSql, new { aUserId, aJobIds = aJobIds.Select(aId => aId.ToString("D")).ToArray() }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vRows.ToList();
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
