using Dapper;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="ISafetyPolicyRepository"/> over the single-row <c>"SafetyPolicy"</c>
/// table (script 0900, SAFE-06 / F-NSFW). Stores only the toggleable NSFW-mode flag; the
/// non-disableable floors are never represented here. Identifiers are quoted PascalCase.
/// </summary>
public class SafetyPolicyRepository : ISafetyPolicyRepository
{
    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public SafetyPolicyRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task<SafetyPolicy?> GetAsync(CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            SELECT "PolicyKey", "NsfwModeEnabled", "UpdatedUtc"
            FROM "SafetyPolicy" WHERE "PolicyKey" = 'default'
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.QuerySingleOrDefaultAsync<SafetyPolicy>(
            new CommandDefinition(Sql, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(SafetyPolicy aPolicy, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            INSERT INTO "SafetyPolicy" ("PolicyKey", "NsfwModeEnabled", "UpdatedUtc")
            VALUES (@aPolicyKey, @aNsfwModeEnabled, @aUpdatedUtc)
            ON CONFLICT ("PolicyKey") DO UPDATE SET
                "NsfwModeEnabled" = EXCLUDED."NsfwModeEnabled",
                "UpdatedUtc" = EXCLUDED."UpdatedUtc"
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(Sql, new
        {
            aPolicyKey = string.IsNullOrWhiteSpace(aPolicy.PolicyKey) ? "default" : aPolicy.PolicyKey,
            aNsfwModeEnabled = aPolicy.NsfwModeEnabled,
            aUpdatedUtc = aPolicy.UpdatedUtc
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }
}
