using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for generated asset metadata. Every read/write of user-owned rows takes the
/// owning AppManager user id and applies the owner filter server-side (BRD-81).
/// </summary>
public interface IAssetRepository
{
    /// <summary>
    /// Inserts a new asset row. The asset's <see cref="GeneratedAsset.UserId"/> records ownership.
    /// </summary>
    /// <param name="aAsset">The asset metadata to insert.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertAsync(GeneratedAsset aAsset, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Gets an asset by id, owner-filtered.
    /// </summary>
    /// <param name="aAssetId">The asset identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The asset, or null when not found or not owned by <paramref name="aUserId"/>.</returns>
    Task<GeneratedAsset?> GetAsync(Guid aAssetId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists the user's assets matching the filter, newest-first, paginated.
    /// </summary>
    /// <param name="aFilter">Search/kind/model/project filters and paging options.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A page of the user's matching assets.</returns>
    Task<PagedResult<GeneratedAsset>> ListAsync(AssetFilterRequest aFilter, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists the assets produced by a single job, owner-filtered.
    /// </summary>
    /// <param name="aJobId">The producing job identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The job's assets owned by <paramref name="aUserId"/>; empty when none exist.</returns>
    Task<IReadOnlyList<GeneratedAsset>> ListByJobAsync(Guid aJobId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Deletes an asset row, owner-filtered.
    /// </summary>
    /// <param name="aAssetId">The asset identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is removed.</returns>
    Task DeleteAsync(Guid aAssetId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Stamps the C2PA-style provenance manifest onto an asset, owner-filtered (SAFE-04 / BRD-63).
    /// </summary>
    /// <param name="aAssetId">The asset identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aProvenanceJson">The provenance manifest JSON to persist.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the manifest is written.</returns>
    Task UpdateProvenanceAsync(Guid aAssetId, string aUserId, string aProvenanceJson, CancellationToken aCancellationToken = default);
}
