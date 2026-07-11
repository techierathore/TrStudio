using TrStudio.Models.Entities;
using TrStudio.Models.Enums;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for the shared face/voice consent evidence (SAFE-03 / BRD-62). Every read/write is
/// owner-scoped by the AppManager user id (BRD-81); rows form part of the SAFE-06 audit trail.
/// </summary>
public interface IConsentRepository
{
    /// <summary>
    /// Appends a consent-evidence row.
    /// </summary>
    /// <param name="aEvidence">The evidence to persist.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertAsync(ConsentEvidence aEvidence, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Determines whether GRANTED consent evidence exists for a subject + modality owned by the user.
    /// </summary>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aSubjectReference">The subject reference to check.</param>
    /// <param name="aModality">The modality (face or voice) the consent must cover.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> when at least one granted-consent row exists; otherwise <c>false</c>.</returns>
    Task<bool> HasGrantedConsentAsync(string aUserId, string aSubjectReference, ConsentModality aModality, CancellationToken aCancellationToken = default);
}
