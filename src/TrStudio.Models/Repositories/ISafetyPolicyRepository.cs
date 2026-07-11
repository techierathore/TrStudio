using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for the single-row per-deployment safety policy (SAFE-06 / BRD-65). Stores only
/// toggleable settings (the NSFW-mode flag); the non-disableable floors are never persisted here.
/// </summary>
public interface ISafetyPolicyRepository
{
    /// <summary>
    /// Gets the deployment policy row, or null when none has been saved yet (defaults apply).
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The stored policy, or null.</returns>
    Task<SafetyPolicy?> GetAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Inserts or updates the single-row deployment policy.
    /// </summary>
    /// <param name="aPolicy">The policy to persist.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task UpsertAsync(SafetyPolicy aPolicy, CancellationToken aCancellationToken = default);
}
