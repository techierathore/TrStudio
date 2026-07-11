using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for model download sources (admin-scoped, instance-global — no per-user filter).
/// API tokens are stored via the encrypted key store, never in plain rows.
/// </summary>
public interface IModelSourceRepository
{
    /// <summary>
    /// Gets a model source by id.
    /// </summary>
    /// <param name="aSourceId">The source identifier.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The source, or null when not found.</returns>
    Task<ModelSource?> GetAsync(Guid aSourceId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists all configured model sources.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>All model sources.</returns>
    Task<IReadOnlyList<ModelSource>> ListAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Creates or replaces a model source row.
    /// </summary>
    /// <param name="aSource">The source definition (token flag only; the token itself goes to the key store).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task UpsertAsync(ModelSource aSource, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Deletes a model source row.
    /// </summary>
    /// <param name="aSourceId">The source identifier.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is removed.</returns>
    Task DeleteAsync(Guid aSourceId, CancellationToken aCancellationToken = default);
}
