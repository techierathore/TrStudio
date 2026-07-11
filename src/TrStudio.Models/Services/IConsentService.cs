using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;

namespace TrStudio.Models.Services;

/// <summary>
/// The shared recorded-consent gate for real-person FACE and VOICE work (SAFE-03 / BRD-62 /
/// REQ-FN-058). Generalises the voice-only consent record so the safety pipeline can gate any
/// real-person generation on retained, auditable evidence. All access is owner-scoped (BRD-81).
/// </summary>
public interface IConsentService
{
    /// <summary>
    /// Records consent evidence for a subject and modality, owner-scoped.
    /// </summary>
    /// <param name="aRequest">The consent evidence to record.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The recorded-evidence result.</returns>
    Task<ConsentRecordResponse> RecordAsync(ConsentRecordRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Determines whether GRANTED consent evidence exists for a subject + modality owned by the user.
    /// </summary>
    /// <param name="aUserId">The owning AppManager user id.</param>
    /// <param name="aSubjectReference">The subject reference to check.</param>
    /// <param name="aModality">The modality (face or voice) the consent must cover.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> when granted consent evidence exists; otherwise <c>false</c> (block).</returns>
    Task<bool> HasConsentAsync(string aUserId, string aSubjectReference, ConsentModality aModality, CancellationToken aCancellationToken = default);
}
