using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for the model and provider registry (Architecture §6). Registry rows are
/// instance-global (admin-owned), so no per-user owner filter applies here.
/// </summary>
public interface IRegistryRepository
{
    /// <summary>
    /// Gets a registry model by id.
    /// </summary>
    /// <param name="aModelId">The registry model identifier.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The model, or null when not registered.</returns>
    Task<RegistryModel?> GetAsync(string aModelId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists registry models.
    /// </summary>
    /// <param name="aEnabledOnly">When true, only models enabled for creators are returned.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The matching registry models.</returns>
    Task<IReadOnlyList<RegistryModel>> ListAsync(bool aEnabledOnly, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Creates or replaces a registry model row.
    /// </summary>
    /// <param name="aModel">The full model definition.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task UpsertAsync(RegistryModel aModel, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Enables or disables a registry model for creators.
    /// </summary>
    /// <param name="aModelId">The registry model identifier.</param>
    /// <param name="aIsEnabled">The new enabled flag.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is updated.</returns>
    Task SetEnabledAsync(string aModelId, bool aIsEnabled, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Deletes a registry model row.
    /// </summary>
    /// <param name="aModelId">The registry model identifier.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is removed.</returns>
    Task DeleteAsync(string aModelId, CancellationToken aCancellationToken = default);
}
