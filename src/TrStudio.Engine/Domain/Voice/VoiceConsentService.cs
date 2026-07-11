using Microsoft.Extensions.Logging;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Voice;

/// <summary>
/// Default <see cref="IVoiceConsentService"/> (FN-032 / SAFE-03 / REQ-FN-058). Persists recorded
/// consent evidence for a real subject and answers whether granted evidence exists — the signal the
/// clone consent gate depends on. All access is owner-scoped (BRD-81).
/// </summary>
public class VoiceConsentService : IVoiceConsentService
{
    private readonly IVoiceConsentRepository objRepository;
    private readonly ILogger<VoiceConsentService> objLogger;

    /// <summary>
    /// Initialises the service.
    /// </summary>
    /// <param name="aRepository">Consent-evidence persistence.</param>
    /// <param name="aLogger">Structured logger (records subject refs, never the evidence payload).</param>
    public VoiceConsentService(IVoiceConsentRepository aRepository, ILogger<VoiceConsentService> aLogger)
    {
        objRepository = aRepository;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<VoiceConsentResponse> RecordConsentAsync(VoiceConsentRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aRequest.SubjectReference))
        {
            return new VoiceConsentResponse
            {
                Recorded = false,
                Message = "SubjectReference is required to record consent evidence."
            };
        }

        var vEvidence = new VoiceConsentEvidence
        {
            EvidenceId = Guid.NewGuid(),
            UserId = aUserId,
            SubjectReference = aRequest.SubjectReference.Trim(),
            EvidenceType = string.IsNullOrWhiteSpace(aRequest.EvidenceType) ? "unspecified" : aRequest.EvidenceType.Trim(),
            EvidenceDetail = aRequest.EvidenceDetail ?? string.Empty,
            ConsentGranted = aRequest.ConsentGranted,
            CreatedUtc = DateTime.UtcNow
        };

        await objRepository.InsertAsync(vEvidence, aCancellationToken).ConfigureAwait(false);

        objLogger.LogInformation(
            "Recorded voice-clone consent evidence {EvidenceId} (granted={Granted}) for subject {Subject}",
            vEvidence.EvidenceId, vEvidence.ConsentGranted, vEvidence.SubjectReference);

        return new VoiceConsentResponse
        {
            EvidenceId = vEvidence.EvidenceId,
            SubjectReference = vEvidence.SubjectReference,
            Recorded = true,
            Message = vEvidence.ConsentGranted
                ? "Consent evidence recorded — real-person cloning for this subject is now unblocked (SAFE-03)."
                : "Refusal recorded — real-person cloning for this subject remains blocked (SAFE-03)."
        };
    }

    /// <inheritdoc />
    public Task<bool> HasConsentAsync(string aUserId, string aSubjectReference, CancellationToken aCancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aSubjectReference))
        {
            return Task.FromResult(false);
        }

        return objRepository.HasGrantedConsentAsync(aUserId, aSubjectReference.Trim(), aCancellationToken);
    }
}
