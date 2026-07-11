using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for registered cloned voices (FN-032). Every read/write is owner-scoped by the
/// AppManager user id (BRD-81).
/// </summary>
public interface IVoiceCloneRepository
{
    /// <summary>
    /// Inserts a registered clone row.
    /// </summary>
    /// <param name="aClone">The clone to persist.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertAsync(VoiceClone aClone, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists the user's registered clones, newest-first.
    /// </summary>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The user's clones; empty when none exist.</returns>
    Task<IReadOnlyList<VoiceClone>> ListAsync(string aUserId, CancellationToken aCancellationToken = default);
}
