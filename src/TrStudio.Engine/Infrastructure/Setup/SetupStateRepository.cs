using Dapper;
using TrStudio.Engine.Domain.Setup;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Models.Entities;

namespace TrStudio.Engine.Infrastructure.Setup;

/// <summary>
/// Dapper <see cref="ISetupStateRepository"/> over the "SetupState" table (script 0300).
/// Quoted PascalCase identifiers, parameterized SQL only.
/// </summary>
public class SetupStateRepository : ISetupStateRepository
{
    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public SetupStateRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SetupStepInfo>> ListAsync(CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            SELECT "SetupStateId" AS "StepKey", "StepName", "State", "ProgressPercent",
                   "Message", "ErrorMessage", "UpdatedUtc"
            FROM "SetupState"
            ORDER BY "StepOrder"
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRows = await vConnection.QueryAsync<SetupStepInfo>(new CommandDefinition(Sql, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vRows.ToList();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(string aStepKey, string aState, int aProgressPercent, string? aMessage, string? aErrorMessage, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            UPDATE "SetupState"
            SET "State" = @aState,
                "ProgressPercent" = @aProgressPercent,
                "Message" = @aMessage,
                "ErrorMessage" = @aErrorMessage,
                "UpdatedUtc" = NOW() AT TIME ZONE 'utc'
            WHERE "SetupStateId" = @aStepKey
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(
            Sql,
            new { aStepKey, aState, aProgressPercent, aMessage, aErrorMessage },
            cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }
}
