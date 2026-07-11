using Dapper;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IProviderKeyRepository"/> over the <c>"ProviderKey"</c> table.
/// Only ciphertext ever reaches this repository — encryption/decryption and masking live in
/// the key store (NFR-05); listings here return name/enabled metadata with an empty mask that
/// the key store fills in.
/// </summary>
public class ProviderKeyRepository : IProviderKeyRepository
{
    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public ProviderKeyRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProviderKeyInfo>> ListAsync(CancellationToken aCancellationToken = default)
    {
        const string vSql =
            "SELECT \"ProviderName\", \"IsEnabled\" FROM \"ProviderKey\" ORDER BY \"ProviderName\"";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRows = await vConnection.QueryAsync<ProviderKeyInfo>(
            new CommandDefinition(vSql, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vRows.ToList();
    }

    /// <inheritdoc />
    public async Task UpsertAsync(string aProviderName, string aEncryptedKey, bool aIsEnabled, CancellationToken aCancellationToken = default)
    {
        const string vSql =
            "INSERT INTO \"ProviderKey\" (\"ProviderName\", \"EncryptedKey\", \"IsEnabled\", \"UpdatedUtc\") " +
            "VALUES (@aProviderName, @aEncryptedKey, @aIsEnabled, now()) " +
            "ON CONFLICT (\"ProviderName\") DO UPDATE SET " +
            "\"EncryptedKey\" = EXCLUDED.\"EncryptedKey\", \"IsEnabled\" = EXCLUDED.\"IsEnabled\", \"UpdatedUtc\" = now()";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(
            vSql, new { aProviderName, aEncryptedKey, aIsEnabled }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string?> GetEncryptedAsync(string aProviderName, CancellationToken aCancellationToken = default)
    {
        const string vSql = "SELECT \"EncryptedKey\" FROM \"ProviderKey\" WHERE \"ProviderName\" = @aProviderName";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(vSql, new { aProviderName }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> SetEnabledAsync(string aProviderName, bool aIsEnabled, CancellationToken aCancellationToken = default)
    {
        const string vSql =
            "UPDATE \"ProviderKey\" SET \"IsEnabled\" = @aIsEnabled, \"UpdatedUtc\" = now() " +
            "WHERE \"ProviderName\" = @aProviderName";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vAffected = await vConnection.ExecuteAsync(new CommandDefinition(
            vSql, new { aProviderName, aIsEnabled }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vAffected > 0;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string aProviderName, CancellationToken aCancellationToken = default)
    {
        const string vSql = "DELETE FROM \"ProviderKey\" WHERE \"ProviderName\" = @aProviderName";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(
            new CommandDefinition(vSql, new { aProviderName }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }
}
