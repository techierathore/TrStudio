using Dapper;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IApiKeyRepository"/> over the <c>"ApiKey"</c> table (REQ-FN-069).
/// Owner-scoped surfaces (list/revoke) apply the <c>"UserId"</c> owner filter (BRD-81); the
/// hash-lookup authentication path is global by necessity (the caller is not yet identified) but
/// only ever matches a single hashed key. Plaintext keys are never stored (NFR-05).
/// </summary>
public class ApiKeyRepository : IApiKeyRepository
{
    private const string SelectColumns =
        "\"ApiKeyId\", \"UserId\", \"Name\", \"Prefix\", \"KeyHash\", \"Scope\", \"CreatedUtc\", " +
        "\"LastUsedUtc\", \"RevokedUtc\"";

    /// <summary>Insert SQL; the row carries its owner in <c>"UserId"</c> and stores only the key hash.</summary>
    internal const string InsertSql =
        "INSERT INTO \"ApiKey\" (\"ApiKeyId\", \"UserId\", \"Name\", \"Prefix\", \"KeyHash\", \"Scope\", " +
        "\"CreatedUtc\", \"LastUsedUtc\", \"RevokedUtc\") " +
        "VALUES (@ApiKeyId, @UserId, @Name, @Prefix, @KeyHash, @Scope, @CreatedUtc, @LastUsedUtc, @RevokedUtc)";

    /// <summary>Authentication lookup: an active (non-revoked) key by hash. Global by necessity.</summary>
    internal const string GetActiveByHashSql =
        "SELECT " + SelectColumns + " FROM \"ApiKey\" WHERE \"KeyHash\" = @aKeyHash AND \"RevokedUtc\" IS NULL";

    /// <summary>Owner-filtered list SQL (BRD-81), newest-first, including revoked rows for the audit view.</summary>
    internal const string ListByUserSql =
        "SELECT " + SelectColumns + " FROM \"ApiKey\" WHERE \"UserId\" = @aUserId ORDER BY \"CreatedUtc\" DESC";

    /// <summary>Last-used touch SQL (by id; the row was already owner-resolved via the hash lookup).</summary>
    internal const string TouchUsedSql =
        "UPDATE \"ApiKey\" SET \"LastUsedUtc\" = @aUsedUtc WHERE \"ApiKeyId\" = @aApiKeyId";

    /// <summary>Owner-filtered revoke SQL (BRD-81); only an unrevoked, owned key is affected.</summary>
    internal const string RevokeSql =
        "UPDATE \"ApiKey\" SET \"RevokedUtc\" = now() " +
        "WHERE \"ApiKeyId\" = @aApiKeyId AND \"UserId\" = @aUserId AND \"RevokedUtc\" IS NULL";

    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public ApiKeyRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(ApiKey aApiKey, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(InsertSql, new
        {
            aApiKey.ApiKeyId,
            aApiKey.UserId,
            aApiKey.Name,
            aApiKey.Prefix,
            aApiKey.KeyHash,
            aApiKey.Scope,
            aApiKey.CreatedUtc,
            aApiKey.LastUsedUtc,
            aApiKey.RevokedUtc
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiKey?> GetActiveByHashAsync(string aKeyHash, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.QuerySingleOrDefaultAsync<ApiKey>(
            new CommandDefinition(GetActiveByHashSql, new { aKeyHash }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApiKey>> ListByUserAsync(string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vItems = await vConnection.QueryAsync<ApiKey>(
            new CommandDefinition(ListByUserSql, new { aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vItems.ToList();
    }

    /// <inheritdoc />
    public async Task TouchUsedAsync(Guid aApiKeyId, DateTime aUsedUtc, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(
            new CommandDefinition(TouchUsedSql, new { aApiKeyId, aUsedUtc }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> RevokeAsync(Guid aApiKeyId, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vAffected = await vConnection.ExecuteAsync(
            new CommandDefinition(RevokeSql, new { aApiKeyId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vAffected > 0;
    }
}
