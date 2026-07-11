using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for model install tracking rows (admin-scoped, instance-global — no per-user filter).
/// </summary>
public interface IModelInstallRepository
{
    /// <summary>
    /// Inserts a new install row.
    /// </summary>
    /// <param name="aInstall">The install item to insert.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertAsync(ModelInstallItem aInstall, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Gets an install row by id.
    /// </summary>
    /// <param name="aInstallId">The install identifier.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The install item, or null when not found.</returns>
    Task<ModelInstallItem?> GetAsync(Guid aInstallId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists all install rows newest-first.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>All install items.</returns>
    Task<IReadOnlyList<ModelInstallItem>> ListAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Updates an install row's progress/state fields.
    /// </summary>
    /// <param name="aInstall">The install item carrying updated values.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is updated.</returns>
    Task UpdateAsync(ModelInstallItem aInstall, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Deletes an install row.
    /// </summary>
    /// <param name="aInstallId">The install identifier.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is removed.</returns>
    Task DeleteAsync(Guid aInstallId, CancellationToken aCancellationToken = default);
}
