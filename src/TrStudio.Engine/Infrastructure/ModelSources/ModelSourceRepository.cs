using Dapper;
using TrStudio.Engine.Domain.Setup;
using TrStudio.Engine.Domain.ModelSources;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.ModelSources;

/// <summary>
/// Dapper <see cref="IModelSourceRepository"/> + <see cref="IModelSourceTokenStore"/> over the
/// "ModelSource" table (script 0301). API tokens are stored AES-GCM-encrypted in the
/// "ApiTokenEncrypted" column and never leave this class in plain text except for internal
/// download authentication (NFR-05). Quoted PascalCase identifiers, parameterized SQL only.
/// </summary>
public class ModelSourceRepository : IModelSourceRepository, IModelSourceTokenStore
{
    private const string SelectColumns = """
        SELECT "ModelSourceId" AS "SourceId", "Name", "Kind", "BaseUrl",
               ("ApiTokenEncrypted" IS NOT NULL) AS "HasToken", "IsEnabled", "Status", "CreatedUtc"
        FROM "ModelSource"
        """;

    private readonly IDbConnectionFactory objConnectionFactory;
    private readonly ISetupSecretProtector objSecretProtector;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    /// <param name="aSecretProtector">AES-GCM protector for source API tokens.</param>
    public ModelSourceRepository(IDbConnectionFactory aConnectionFactory, ISetupSecretProtector aSecretProtector)
    {
        objConnectionFactory = aConnectionFactory;
        objSecretProtector = aSecretProtector;
    }

    /// <inheritdoc />
    public async Task<ModelSource?> GetAsync(Guid aSourceId, CancellationToken aCancellationToken = default)
    {
        const string Sql = SelectColumns + " WHERE \"ModelSourceId\" = @aSourceId";
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.QuerySingleOrDefaultAsync<ModelSource>(
            new CommandDefinition(Sql, new { aSourceId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModelSource>> ListAsync(CancellationToken aCancellationToken = default)
    {
        const string Sql = SelectColumns + " ORDER BY \"CreatedUtc\"";
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRows = await vConnection.QueryAsync<ModelSource>(
            new CommandDefinition(Sql, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vRows.ToList();
    }

    /// <inheritdoc />
    public async Task UpsertAsync(ModelSource aSource, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            INSERT INTO "ModelSource" ("ModelSourceId", "Name", "Kind", "BaseUrl", "IsEnabled", "Status", "CreatedUtc")
            VALUES (@aSourceId, @aName, @aKind, @aBaseUrl, @aIsEnabled, @aStatus, NOW() AT TIME ZONE 'utc')
            ON CONFLICT ("ModelSourceId") DO UPDATE SET
                "Name" = EXCLUDED."Name",
                "Kind" = EXCLUDED."Kind",
                "BaseUrl" = EXCLUDED."BaseUrl",
                "IsEnabled" = EXCLUDED."IsEnabled",
                "Status" = EXCLUDED."Status"
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(
            Sql,
            new
            {
                aSourceId = aSource.SourceId,
                aName = aSource.Name,
                aKind = aSource.Kind.ToString(),
                aBaseUrl = aSource.BaseUrl,
                aIsEnabled = aSource.IsEnabled,
                aStatus = string.IsNullOrWhiteSpace(aSource.Status) ? "unknown" : aSource.Status
            },
            cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid aSourceId, CancellationToken aCancellationToken = default)
    {
        const string Sql = "DELETE FROM \"ModelSource\" WHERE \"ModelSourceId\" = @aSourceId";
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(Sql, new { aSourceId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetTokenAsync(Guid aSourceId, string? aPlainToken, CancellationToken aCancellationToken = default)
    {
        const string Sql = "UPDATE \"ModelSource\" SET \"ApiTokenEncrypted\" = @aTokenEncrypted WHERE \"ModelSourceId\" = @aSourceId";
        var vTokenEncrypted = string.IsNullOrWhiteSpace(aPlainToken) ? null : objSecretProtector.Protect(aPlainToken);
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(
            Sql, new { aSourceId, aTokenEncrypted = vTokenEncrypted }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string?> GetTokenAsync(Guid aSourceId, CancellationToken aCancellationToken = default)
    {
        const string Sql = "SELECT \"ApiTokenEncrypted\" FROM \"ModelSource\" WHERE \"ModelSourceId\" = @aSourceId";
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vEncrypted = await vConnection.ExecuteScalarAsync<string?>(
            new CommandDefinition(Sql, new { aSourceId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vEncrypted is null ? null : objSecretProtector.Unprotect(vEncrypted);
    }
}
