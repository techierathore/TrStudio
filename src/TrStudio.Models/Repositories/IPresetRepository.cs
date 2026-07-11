using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for the creator preset library (REQ-FN-066/BRD-50). EVERY read/write applies the
/// <c>"UserId"</c> owner filter server-side so a creator only ever sees or mutates their own
/// presets (BRD-81).
/// </summary>
public interface IPresetRepository
{
    /// <summary>
    /// Inserts a new preset; the row carries its owner in <see cref="Preset.UserId"/>.
    /// </summary>
    /// <param name="aPreset">The preset to persist.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertAsync(Preset aPreset, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Gets one preset by id, owner-filtered.
    /// </summary>
    /// <param name="aPresetId">The preset identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The preset, or null when not found or not owned by <paramref name="aUserId"/>.</returns>
    Task<Preset?> GetAsync(Guid aPresetId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists the user's presets newest-first, optionally filtered by type.
    /// </summary>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aType">Optional preset type filter (<c>prompt</c>/<c>script</c>/<c>style-preset</c>/<c>voice-preset</c>).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The user's presets.</returns>
    Task<IReadOnlyList<Preset>> ListAsync(string aUserId, string? aType, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Updates a preset's editable fields (name, description, content), owner-filtered.
    /// </summary>
    /// <param name="aPreset">The preset carrying the new values.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the update is applied.</returns>
    Task UpdateAsync(Preset aPreset, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Atomically increments the preset's use count and returns the updated row, owner-filtered.
    /// </summary>
    /// <param name="aPresetId">The preset identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated preset, or null when not found or not owned.</returns>
    Task<Preset?> IncrementUseAsync(Guid aPresetId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Deletes a preset, owner-filtered.
    /// </summary>
    /// <param name="aPresetId">The preset identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the delete is applied.</returns>
    Task DeleteAsync(Guid aPresetId, string aUserId, CancellationToken aCancellationToken = default);
}
