using Dapper;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>Reads and writes the app's own key/value settings ("AppSetting" table, script 1730).</summary>
public interface IAppSettingStore
{
    /// <summary>Gets a setting.</summary>
    /// <param name="aKey">The setting key.</param>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>The value, or null when not set.</returns>
    Task<string?> GetAsync(string aKey, CancellationToken aCancellationToken = default);

    /// <summary>Sets a setting, replacing any earlier value.</summary>
    /// <param name="aKey">The setting key.</param>
    /// <param name="aValue">The value.</param>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    Task SetAsync(string aKey, string aValue, CancellationToken aCancellationToken = default);
}

/// <summary>Dapper <see cref="IAppSettingStore"/> over the local SQLite database.</summary>
public class AppSettingStore : IAppSettingStore
{
    /// <summary>Key of the address of the ComfyUI that runs on its own (ADR-029).</summary>
    public const string ComfyUiBaseUrlKey = "ComfyUI:BaseUrl";

    /// <summary>Key of the days log rows are kept (ADR-030).</summary>
    public const string LogRetentionDaysKey = "Retention:LogDays";

    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>Creates the store.</summary>
    /// <param name="aConnectionFactory">Factory of open connections.</param>
    public AppSettingStore(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string aKey, CancellationToken aCancellationToken = default)
    {
        const string Sql = """SELECT "SettingValue" FROM "AppSetting" WHERE "SettingKey" = @aKey""";
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.QueryFirstOrDefaultAsync<string?>(new CommandDefinition(Sql, new { aKey }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetAsync(string aKey, string aValue, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            INSERT INTO "AppSetting" ("SettingKey", "SettingValue", "UpdatedUtc")
            VALUES (@aKey, @aValue, strftime('%Y-%m-%dT%H:%M:%fZ','now'))
            ON CONFLICT ("SettingKey") DO UPDATE SET "SettingValue" = excluded."SettingValue", "UpdatedUtc" = excluded."UpdatedUtc"
            """;
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(Sql, new { aKey, aValue }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }
}
