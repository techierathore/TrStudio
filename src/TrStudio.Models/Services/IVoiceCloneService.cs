using TrStudio.Models.Contracts;

namespace TrStudio.Models.Services;

/// <summary>
/// Registers cloned voices behind the consent gate (FN-032/BRD-23). Real-person clones are blocked
/// until recorded-consent evidence exists (SAFE-03); synthetic clones proceed ungated.
/// </summary>
public interface IVoiceCloneService
{
    /// <summary>
    /// Requests a voice clone, applying the consent gate for real-person voices.
    /// </summary>
    /// <param name="aRequest">The clone request.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The clone result — blocked (no consent) or registered.</returns>
    Task<VoiceCloneResponse> RequestCloneAsync(VoiceCloneRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);
}
