using Dapper;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Install;

/// <summary>
/// Dapper <see cref="IModelInstallRepository"/> over the "ModelInstall" table (script 0302).
/// Quoted PascalCase identifiers, parameterized SQL only.
/// </summary>
public class ModelInstallRepository : IModelInstallRepository
{
    private const string SelectColumns = """
        SELECT "ModelInstallId" AS "InstallId", "ModelName", "ModelSourceId" AS "SourceId",
               "DownloadUrl", "TotalBytes", "DownloadedBytes", "State", "Destination",
               "SpeedBytesPerSec", "LicenseName", "MinVramGb", "ErrorMessage", "CreatedUtc"
        FROM "ModelInstall"
        """;

    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public ModelInstallRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(ModelInstallItem aInstall, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            INSERT INTO "ModelInstall"
                ("ModelInstallId", "ModelName", "ModelSourceId", "DownloadUrl", "TotalBytes", "DownloadedBytes",
                 "State", "Destination", "SpeedBytesPerSec", "LicenseName", "MinVramGb", "ErrorMessage",
                 "CreatedUtc", "UpdatedUtc")
            VALUES
                (@aInstallId, @aModelName, @aSourceId, @aDownloadUrl, @aTotalBytes, @aDownloadedBytes,
                 @aState, @aDestination, @aSpeedBytesPerSec, @aLicenseName, @aMinVramGb, @aErrorMessage,
                 NOW() AT TIME ZONE 'utc', NOW() AT TIME ZONE 'utc')
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(Sql, ToParameters(aInstall), cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ModelInstallItem?> GetAsync(Guid aInstallId, CancellationToken aCancellationToken = default)
    {
        const string Sql = SelectColumns + " WHERE \"ModelInstallId\" = @aInstallId";
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.QuerySingleOrDefaultAsync<ModelInstallItem>(
            new CommandDefinition(Sql, new { aInstallId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModelInstallItem>> ListAsync(CancellationToken aCancellationToken = default)
    {
        const string Sql = SelectColumns + " ORDER BY \"CreatedUtc\" DESC";
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRows = await vConnection.QueryAsync<ModelInstallItem>(
            new CommandDefinition(Sql, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vRows.ToList();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ModelInstallItem aInstall, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            UPDATE "ModelInstall" SET
                "TotalBytes" = @aTotalBytes,
                "DownloadedBytes" = @aDownloadedBytes,
                "State" = @aState,
                "Destination" = @aDestination,
                "SpeedBytesPerSec" = @aSpeedBytesPerSec,
                "LicenseName" = @aLicenseName,
                "MinVramGb" = @aMinVramGb,
                "ErrorMessage" = @aErrorMessage,
                "UpdatedUtc" = NOW() AT TIME ZONE 'utc'
            WHERE "ModelInstallId" = @aInstallId
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(Sql, ToParameters(aInstall), cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid aInstallId, CancellationToken aCancellationToken = default)
    {
        const string Sql = "DELETE FROM \"ModelInstall\" WHERE \"ModelInstallId\" = @aInstallId";
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(Sql, new { aInstallId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    private static object ToParameters(ModelInstallItem aInstall) => new
    {
        aInstallId = aInstall.InstallId,
        aModelName = aInstall.ModelName,
        aSourceId = aInstall.SourceId,
        aDownloadUrl = aInstall.DownloadUrl,
        aTotalBytes = aInstall.TotalBytes,
        aDownloadedBytes = aInstall.DownloadedBytes,
        aState = aInstall.State,
        aDestination = aInstall.Destination,
        aSpeedBytesPerSec = aInstall.SpeedBytesPerSec,
        aLicenseName = aInstall.LicenseName,
        aMinVramGb = aInstall.MinVramGb,
        aErrorMessage = aInstall.ErrorMessage
    };
}
