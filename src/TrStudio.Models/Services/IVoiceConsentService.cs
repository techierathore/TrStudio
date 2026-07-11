using TrStudio.Models.Contracts;

namespace TrStudio.Models.Services;

/// <summary>
/// Records and checks recorded-consent evidence for real-person voice cloning (FN-032 / SAFE-03 /
/// REQ-FN-058).
/// </summary>
public interface IVoiceConsentService
{
    /// <summary>
    /// Records consent evidence for a subject, owner-scoped.
    /// </summary>
    /// <param name="aRequest">The consent evidence to record.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The recorded-evidence result.</returns>
    Task<VoiceConsentResponse> RecordConsentAsync(VoiceConsentRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Determines whether granted consent evidence exists for a subject owned by the given user.
    /// </summary>
    /// <param name="aUserId">The owning AppManager user id.</param>
    /// <param name="aSubjectReference">The subject reference to check.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> when granted consent evidence exists; otherwise <c>false</c>.</returns>
    Task<bool> HasConsentAsync(string aUserId, string aSubjectReference, CancellationToken aCancellationToken = default);
}
