using System.Text;
using Dapper;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IAssetRepository"/> over the <c>"GeneratedAsset"</c> table.
/// Every query applies the <c>"UserId"</c> owner filter server-side (BRD-81).
/// </summary>
public class AssetRepository : IAssetRepository
{
    private const string SelectColumns =
        "\"GeneratedAssetId\" AS \"AssetId\", \"GenerationJobId\" AS \"JobId\", \"UserId\", \"ProjectId\", " +
        "\"Kind\", \"StoragePath\", \"ThumbnailPath\", \"Width\", \"Height\", \"SeedUsed\", \"ParamsJson\", " +
        "\"ModelId\", \"ProvenanceJson\", \"CreatedUtc\"";

    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public AssetRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(GeneratedAsset aAsset, CancellationToken aCancellationToken = default)
    {
        const string vSql =
            "INSERT INTO \"GeneratedAsset\" (\"GeneratedAssetId\", \"GenerationJobId\", \"UserId\", \"ProjectId\", " +
            "\"Kind\", \"StoragePath\", \"ThumbnailPath\", \"Width\", \"Height\", \"SeedUsed\", \"ParamsJson\", " +
            "\"ModelId\", \"ProvenanceJson\", \"CreatedUtc\") " +
            "VALUES (@AssetId, @JobId, @UserId, @ProjectId, @Kind, @StoragePath, @ThumbnailPath, @Width, @Height, " +
            "@SeedUsed, CAST(@ParamsJson AS jsonb), @ModelId, CAST(@ProvenanceJson AS jsonb), @CreatedUtc)";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(vSql, new
        {
            aAsset.AssetId,
            aAsset.JobId,
            aAsset.UserId,
            aAsset.ProjectId,
            aAsset.Kind,
            aAsset.StoragePath,
            aAsset.ThumbnailPath,
            aAsset.Width,
            aAsset.Height,
            aAsset.SeedUsed,
            aAsset.ParamsJson,
            aAsset.ModelId,
            aAsset.ProvenanceJson,
            aAsset.CreatedUtc
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GeneratedAsset?> GetAsync(Guid aAssetId, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vSql = $"SELECT {SelectColumns} FROM \"GeneratedAsset\" WHERE \"GeneratedAssetId\" = @aAssetId AND \"UserId\" = @aUserId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.QuerySingleOrDefaultAsync<GeneratedAsset>(
            new CommandDefinition(vSql, new { aAssetId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PagedResult<GeneratedAsset>> ListAsync(AssetFilterRequest aFilter, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vPage = Math.Max(1, aFilter.Page);
        var vPageSize = Math.Clamp(aFilter.PageSize, 1, 200);

        var vWhere = new StringBuilder("WHERE \"UserId\" = @aUserId");
        if (!string.IsNullOrWhiteSpace(aFilter.Kind))
        {
            vWhere.Append(" AND \"Kind\" = @aKind");
        }

        if (!string.IsNullOrWhiteSpace(aFilter.ModelId))
        {
            vWhere.Append(" AND \"ModelId\" = @aModelId");
        }

        if (aFilter.ProjectId.HasValue)
        {
            vWhere.Append(" AND \"ProjectId\" = @aProjectId");
        }

        if (!string.IsNullOrWhiteSpace(aFilter.Search))
        {
            vWhere.Append(" AND \"ParamsJson\"::text ILIKE @aSearch");
        }

        var vSql =
            $"SELECT {SelectColumns} FROM \"GeneratedAsset\" {vWhere} " +
            "ORDER BY \"CreatedUtc\" DESC LIMIT @aLimit OFFSET @aOffset; " +
            $"SELECT COUNT(*) FROM \"GeneratedAsset\" {vWhere}";

        var vParams = new
        {
            aUserId,
            aKind = aFilter.Kind,
            aModelId = aFilter.ModelId,
            aProjectId = aFilter.ProjectId,
            aSearch = $"%{aFilter.Search}%",
            aLimit = vPageSize,
            aOffset = (vPage - 1) * vPageSize
        };

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        using var vMulti = await vConnection.QueryMultipleAsync(
            new CommandDefinition(vSql, vParams, cancellationToken: aCancellationToken)).ConfigureAwait(false);

        var vItems = (await vMulti.ReadAsync<GeneratedAsset>().ConfigureAwait(false)).ToList();
        var vTotal = await vMulti.ReadSingleAsync<int>().ConfigureAwait(false);
        return new PagedResult<GeneratedAsset> { Items = vItems, TotalCount = vTotal, Page = vPage, PageSize = vPageSize };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GeneratedAsset>> ListByJobAsync(Guid aJobId, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vSql = $"SELECT {SelectColumns} FROM \"GeneratedAsset\" WHERE \"GenerationJobId\" = @aJobId AND \"UserId\" = @aUserId ORDER BY \"CreatedUtc\"";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vItems = await vConnection.QueryAsync<GeneratedAsset>(
            new CommandDefinition(vSql, new { aJobId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vItems.ToList();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid aAssetId, string aUserId, CancellationToken aCancellationToken = default)
    {
        const string vSql = "DELETE FROM \"GeneratedAsset\" WHERE \"GeneratedAssetId\" = @aAssetId AND \"UserId\" = @aUserId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(
            new CommandDefinition(vSql, new { aAssetId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateProvenanceAsync(Guid aAssetId, string aUserId, string aProvenanceJson, CancellationToken aCancellationToken = default)
    {
        const string vSql =
            "UPDATE \"GeneratedAsset\" SET \"ProvenanceJson\" = CAST(@aProvenanceJson AS jsonb) " +
            "WHERE \"GeneratedAssetId\" = @aAssetId AND \"UserId\" = @aUserId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(
            new CommandDefinition(vSql, new { aAssetId, aUserId, aProvenanceJson }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }
}
