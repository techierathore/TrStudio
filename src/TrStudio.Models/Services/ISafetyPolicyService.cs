using TrStudio.Models.Contracts;

namespace TrStudio.Models.Services;

/// <summary>
/// The per-deployment safety-policy surface (SAFE-06 / BRD-65) governing the owner-only
/// Unrestricted (NSFW) mode (F-NSFW / BRD-79). The mode is off by default and only ever effective
/// in personal/single-user mode; it is forced off in multi-user/SaaS. It can NEVER lift the
/// non-disableable floors (SAFE-01..06).
/// </summary>
public interface ISafetyPolicyService
{
    /// <summary>
    /// Gets the effective policy state (stored flag, personal-mode, and the resolved effective mode).
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The effective NSFW-mode state.</returns>
    Task<NsfwPolicyResponse> GetNsfwPolicyAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Sets the owner's NSFW-mode preference. Enabling is refused (forced off) in multi-user/SaaS mode.
    /// </summary>
    /// <param name="aEnabled">Whether the owner wants the mode enabled.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The resulting effective policy state.</returns>
    Task<NsfwPolicyResponse> SetNsfwEnabledAsync(bool aEnabled, CancellationToken aCancellationToken = default);
}
