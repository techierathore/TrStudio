using Dapper;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IPresetRepository"/> over the <c>"Preset"</c> table (REQ-FN-066).
/// EVERY read/write applies the <c>"UserId"</c> owner filter server-side so a creator only ever
/// touches their own presets (BRD-81). The SQL is exposed as internal constants so the
/// owner-filter invariant is unit-tested without a live database.
/// </summary>
public class PresetRepository : IPresetRepository
{
    private const string SelectColumns =
        "\"PresetId\", \"UserId\", \"Type\", \"Name\", \"Description\", \"Content\", \"UseCount\", " +
        "\"CreatedUtc\", \"UpdatedUtc\"";

    /// <summary>Insert SQL; the row carries its owner in <c>"UserId"</c>.</summary>
    internal const string InsertSql =
        "INSERT INTO \"Preset\" (\"PresetId\", \"UserId\", \"Type\", \"Name\", \"Description\", \"Content\", " +
        "\"UseCount\", \"CreatedUtc\", \"UpdatedUtc\") " +
        "VALUES (@PresetId, @UserId, @Type, @Name, @Description, @Content, @UseCount, @CreatedUtc, @UpdatedUtc)";

    /// <summary>Owner-filtered single-preset read SQL (BRD-81).</summary>
    internal const string GetSql =
        "SELECT " + SelectColumns + " FROM \"Preset\" WHERE \"PresetId\" = @aPresetId AND \"UserId\" = @aUserId";

    /// <summary>Owner-filtered list SQL (BRD-81); an optional type filter is applied when supplied.</summary>
    internal const string ListSql =
        "SELECT " + SelectColumns + " FROM \"Preset\" WHERE \"UserId\" = @aUserId " +
        "AND (@aType IS NULL OR \"Type\" = @aType) ORDER BY \"UpdatedUtc\" DESC";

    /// <summary>Owner-filtered update SQL (BRD-81).</summary>
    internal const string UpdateSql =
        "UPDATE \"Preset\" SET \"Name\" = @Name, \"Description\" = @Description, \"Content\" = @Content, " +
        "\"UpdatedUtc\" = @UpdatedUtc WHERE \"PresetId\" = @PresetId AND \"UserId\" = @aUserId";

    /// <summary>Owner-filtered atomic use-count bump returning the updated row (BRD-81).</summary>
    internal const string IncrementUseSql =
        "UPDATE \"Preset\" SET \"UseCount\" = \"UseCount\" + 1, \"UpdatedUtc\" = now() " +
        "WHERE \"PresetId\" = @aPresetId AND \"UserId\" = @aUserId " +
        "RETURNING " + SelectColumns;

    /// <summary>Owner-filtered delete SQL (BRD-81).</summary>
    internal const string DeleteSql =
        "DELETE FROM \"Preset\" WHERE \"PresetId\" = @aPresetId AND \"UserId\" = @aUserId";

    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public PresetRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(Preset aPreset, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(InsertSql, new
        {
            aPreset.PresetId,
            aPreset.UserId,
            aPreset.Type,
            aPreset.Name,
            aPreset.Description,
            aPreset.Content,
            aPreset.UseCount,
            aPreset.CreatedUtc,
            aPreset.UpdatedUtc
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Preset?> GetAsync(Guid aPresetId, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.QuerySingleOrDefaultAsync<Preset>(
            new CommandDefinition(GetSql, new { aPresetId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Preset>> ListAsync(string aUserId, string? aType, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vItems = await vConnection.QueryAsync<Preset>(
            new CommandDefinition(ListSql, new { aUserId, aType }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vItems.ToList();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Preset aPreset, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(UpdateSql, new
        {
            aPreset.PresetId,
            aPreset.Name,
            aPreset.Description,
            aPreset.Content,
            aPreset.UpdatedUtc,
            aUserId
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Preset?> IncrementUseAsync(Guid aPresetId, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.QuerySingleOrDefaultAsync<Preset>(
            new CommandDefinition(IncrementUseSql, new { aPresetId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid aPresetId, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(
            new CommandDefinition(DeleteSql, new { aPresetId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }
}
