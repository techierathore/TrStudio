using Dapper;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IDigitalTwinRepository"/> over the <c>"DigitalTwin"</c> table (script 1710).
/// Every read and write applies the <c>"UserId"</c> owner filter (BRD-81).
/// </summary>
public class DigitalTwinRepository : IDigitalTwinRepository
{
    private const string SelectColumns =
        "\"DigitalTwinId\", \"UserId\", \"Name\", \"SubjectReference\", \"PictureAssetIdsJson\", \"ClipAssetId\", " +
        "\"ClipSeconds\", \"VoiceSampleAssetId\", \"VeniceVoiceId\", \"VoiceCreatedUtc\", \"CreatedUtc\"";

    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>Initialises the repository.</summary>
    /// <param name="aConnectionFactory">Factory producing open connections.</param>
    public DigitalTwinRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(DigitalTwin aTwin, CancellationToken aCancellationToken = default)
    {
        const string Sql =
            "INSERT INTO \"DigitalTwin\" (" + SelectColumns + ") " +
            "VALUES (@DigitalTwinId, @UserId, @Name, @SubjectReference, @PictureAssetIdsJson, @ClipAssetId, " +
            "@ClipSeconds, @VoiceSampleAssetId, @VeniceVoiceId, @VoiceCreatedUtc, @CreatedUtc)";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(Sql, aTwin, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DigitalTwin?> GetAsync(Guid aTwinId, string aUserId, CancellationToken aCancellationToken = default)
    {
        const string Sql =
            "SELECT " + SelectColumns + " FROM \"DigitalTwin\" WHERE \"DigitalTwinId\" = @aTwinId AND \"UserId\" = @aUserId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.QuerySingleOrDefaultAsync<DigitalTwin>(
            new CommandDefinition(Sql, new { aTwinId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DigitalTwin>> ListAsync(string aUserId, CancellationToken aCancellationToken = default)
    {
        const string Sql =
            "SELECT " + SelectColumns + " FROM \"DigitalTwin\" WHERE \"UserId\" = @aUserId ORDER BY \"CreatedUtc\" DESC";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRows = await vConnection.QueryAsync<DigitalTwin>(
            new CommandDefinition(Sql, new { aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vRows.ToList();
    }

    /// <inheritdoc />
    public async Task UpdateVoiceAsync(Guid aTwinId, string aUserId, string aVoiceId, DateTime aVoiceCreatedUtc, CancellationToken aCancellationToken = default)
    {
        const string Sql =
            "UPDATE \"DigitalTwin\" SET \"VeniceVoiceId\" = @aVoiceId, \"VoiceCreatedUtc\" = @aVoiceCreatedUtc " +
            "WHERE \"DigitalTwinId\" = @aTwinId AND \"UserId\" = @aUserId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(
            Sql, new { aTwinId, aUserId, aVoiceId, aVoiceCreatedUtc }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }
}
