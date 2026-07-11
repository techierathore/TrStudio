using Dapper;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="ISpendRepository"/> over the <c>"SpendRecord"</c> table (script 0700,
/// REQ-FN-029). The monthly cap is instance-global, so the aggregate queries are NOT
/// owner-filtered; the parameterised SQL quotes all PascalCase identifiers.
/// </summary>
public class SpendRepository : ISpendRepository
{
    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public SpendRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(SpendRecord aRecord, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            INSERT INTO "SpendRecord" ("SpendRecordId", "UserId", "GenerationJobId", "ModelId", "Channel", "AmountUsd", "CreatedUtc")
            VALUES (@aSpendRecordId, @aUserId, @aJobId, @aModelId, @aChannel, @aAmountUsd, @aCreatedUtc)
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(
            Sql,
            new
            {
                aSpendRecordId = aRecord.SpendRecordId,
                aUserId = aRecord.UserId,
                aJobId = aRecord.JobId,
                aModelId = aRecord.ModelId,
                aChannel = aRecord.Channel.ToString(),
                aAmountUsd = aRecord.AmountUsd,
                aCreatedUtc = aRecord.CreatedUtc
            },
            cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<decimal> GetTotalSinceAsync(DateTime aSinceUtc, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            SELECT COALESCE(SUM("AmountUsd"), 0) FROM "SpendRecord" WHERE "CreatedUtc" >= @aSinceUtc
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(Sql, new { aSinceUtc }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<SpendChannel, decimal>> GetTotalsByChannelSinceAsync(DateTime aSinceUtc, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            SELECT "Channel", COALESCE(SUM("AmountUsd"), 0) AS "Total"
            FROM "SpendRecord"
            WHERE "CreatedUtc" >= @aSinceUtc
            GROUP BY "Channel"
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRows = await vConnection.QueryAsync<ChannelTotalRow>(
            new CommandDefinition(Sql, new { aSinceUtc }, cancellationToken: aCancellationToken)).ConfigureAwait(false);

        var vResult = new Dictionary<SpendChannel, decimal>();
        foreach (var vRow in vRows)
        {
            if (Enum.TryParse<SpendChannel>(vRow.Channel, out var vChannel))
            {
                vResult[vChannel] = vRow.Total;
            }
        }

        return vResult;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<SpendChannel, decimal>> GetTotalsByChannelForUserSinceAsync(string aUserId, DateTime aSinceUtc, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            SELECT "Channel", COALESCE(SUM("AmountUsd"), 0) AS "Total"
            FROM "SpendRecord"
            WHERE "UserId" = @aUserId AND "CreatedUtc" >= @aSinceUtc
            GROUP BY "Channel"
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRows = await vConnection.QueryAsync<ChannelTotalRow>(
            new CommandDefinition(Sql, new { aUserId, aSinceUtc }, cancellationToken: aCancellationToken)).ConfigureAwait(false);

        var vResult = new Dictionary<SpendChannel, decimal>();
        foreach (var vRow in vRows)
        {
            if (Enum.TryParse<SpendChannel>(vRow.Channel, out var vChannel))
            {
                vResult[vChannel] = vRow.Total;
            }
        }

        return vResult;
    }

    /// <summary>Flat Dapper row for the per-channel spend aggregate.</summary>
    private sealed class ChannelTotalRow
    {
        public string Channel { get; set; } = string.Empty;
        public decimal Total { get; set; }
    }
}
