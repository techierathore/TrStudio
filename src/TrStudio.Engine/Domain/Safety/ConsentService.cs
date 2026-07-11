using Microsoft.Extensions.Logging;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Safety;

/// <summary>
/// Default shared <see cref="IConsentService"/> (SAFE-03 / BRD-62 / REQ-FN-058). Generalises the
/// voice-only consent record to cover real-person FACE and VOICE work: it persists recorded consent
/// evidence and answers whether granted evidence exists for a subject + modality — the signal the
/// safety pipeline's consent gate depends on. All access is owner-scoped (BRD-81); evidence is
/// retained and auditable (SAFE-06).
/// </summary>
public class ConsentService : IConsentService
{
    private readonly IConsentRepository objRepository;
    private readonly ILogger<ConsentService> objLogger;

    /// <summary>
    /// Initialises the service.
    /// </summary>
    /// <param name="aRepository">Consent-evidence persistence.</param>
    /// <param name="aLogger">Structured logger (records subject refs, never the evidence payload).</param>
    public ConsentService(IConsentRepository aRepository, ILogger<ConsentService> aLogger)
    {
        objRepository = aRepository;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<ConsentRecordResponse> RecordAsync(ConsentRecordRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aRequest.SubjectReference))
        {
            return new ConsentRecordResponse
            {
                Recorded = false,
                Message = "SubjectReference is required to record consent evidence."
            };
        }

        var vEvidence = new ConsentEvidence
        {
            EvidenceId = Guid.NewGuid(),
            UserId = aUserId,
            SubjectReference = aRequest.SubjectReference.Trim(),
            Modality = aRequest.Modality,
            EvidenceType = string.IsNullOrWhiteSpace(aRequest.EvidenceType) ? "unspecified" : aRequest.EvidenceType.Trim(),
            EvidenceDetail = aRequest.EvidenceDetail ?? string.Empty,
            ConsentGranted = aRequest.ConsentGranted,
            CreatedUtc = DateTime.UtcNow
        };

        await objRepository.InsertAsync(vEvidence, aCancellationToken).ConfigureAwait(false);

        objLogger.LogInformation(
            "Recorded {Modality} consent evidence {EvidenceId} (granted={Granted}) for subject {Subject}",
            vEvidence.Modality, vEvidence.EvidenceId, vEvidence.ConsentGranted, vEvidence.SubjectReference);

        return new ConsentRecordResponse
        {
            EvidenceId = vEvidence.EvidenceId,
            SubjectReference = vEvidence.SubjectReference,
            Recorded = true,
            Message = vEvidence.ConsentGranted
                ? $"Consent evidence recorded — real-person {vEvidence.Modality.ToString().ToLowerInvariant()} work for this subject is now unblocked (SAFE-03)."
                : $"Refusal recorded — real-person {vEvidence.Modality.ToString().ToLowerInvariant()} work for this subject remains blocked (SAFE-03)."
        };
    }

    /// <inheritdoc />
    public Task<bool> HasConsentAsync(string aUserId, string aSubjectReference, ConsentModality aModality, CancellationToken aCancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aSubjectReference))
        {
            return Task.FromResult(false);
        }

        return objRepository.HasGrantedConsentAsync(aUserId, aSubjectReference.Trim(), aModality, aCancellationToken);
    }
}
