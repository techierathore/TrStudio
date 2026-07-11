using Microsoft.Extensions.Logging;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Voice;

/// <summary>
/// Default <see cref="IVoiceCloneService"/> (FN-032/BRD-23). Enforces the SAFE-03 consent gate:
/// cloning a REAL person is BLOCKED until granted consent evidence exists for the subject, while a
/// fully-synthetic clone proceeds ungated. A permitted clone is registered (Dapper); actual model
/// training is deferred until a voice model is installed.
/// </summary>
public class VoiceCloneService : IVoiceCloneService
{
    private readonly IVoiceConsentService objConsentService;
    private readonly IVoiceCloneRepository objRepository;
    private readonly ILogger<VoiceCloneService> objLogger;

    /// <summary>
    /// Initialises the service.
    /// </summary>
    /// <param name="aConsentService">The consent gate the real-person path depends on.</param>
    /// <param name="aRepository">Clone-registration persistence.</param>
    /// <param name="aLogger">Structured logger.</param>
    public VoiceCloneService(
        IVoiceConsentService aConsentService,
        IVoiceCloneRepository aRepository,
        ILogger<VoiceCloneService> aLogger)
    {
        objConsentService = aConsentService;
        objRepository = aRepository;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<VoiceCloneResponse> RequestCloneAsync(VoiceCloneRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aRequest.VoiceName))
        {
            return new VoiceCloneResponse
            {
                Blocked = true,
                Status = "Rejected",
                Message = "VoiceName is required."
            };
        }

        // SAFE-03 consent gate — only the real-person path is gated.
        if (aRequest.IsRealPerson)
        {
            if (string.IsNullOrWhiteSpace(aRequest.SubjectReference))
            {
                objLogger.LogWarning("Real-person voice clone blocked: no SubjectReference supplied for consent lookup");
                return new VoiceCloneResponse
                {
                    Blocked = true,
                    ConsentVerified = false,
                    IsSynthetic = false,
                    Status = "BlockedNoConsent",
                    Message = "Cloning a real person requires a SubjectReference and recorded consent evidence (SAFE-03). " +
                              "Record consent via POST /api/v1/voice/consent first."
                };
            }

            var vHasConsent = await objConsentService
                .HasConsentAsync(aUserId, aRequest.SubjectReference, aCancellationToken)
                .ConfigureAwait(false);

            if (!vHasConsent)
            {
                objLogger.LogWarning(
                    "Real-person voice clone BLOCKED for subject {Subject}: no granted consent evidence (SAFE-03)",
                    aRequest.SubjectReference);
                return new VoiceCloneResponse
                {
                    Blocked = true,
                    ConsentVerified = false,
                    IsSynthetic = false,
                    Status = "BlockedNoConsent",
                    Message = "BLOCKED — no recorded consent evidence for this subject. Real-person voice cloning is " +
                              "consent-gated (SAFE-03). Record consent via POST /api/v1/voice/consent, then retry."
                };
            }
        }

        var vClone = new VoiceClone
        {
            CloneId = Guid.NewGuid(),
            UserId = aUserId,
            VoiceName = aRequest.VoiceName.Trim(),
            Language = string.IsNullOrWhiteSpace(aRequest.Language) ? "en" : aRequest.Language.Trim(),
            IsRealPerson = aRequest.IsRealPerson,
            SubjectReference = aRequest.IsRealPerson ? aRequest.SubjectReference?.Trim() : null,
            Status = "ConsentVerifiedModelPending",
            CreatedUtc = DateTime.UtcNow
        };

        await objRepository.InsertAsync(vClone, aCancellationToken).ConfigureAwait(false);

        objLogger.LogInformation(
            "Registered voice clone {CloneId} (realPerson={Real}, synthetic={Synthetic})",
            vClone.CloneId, aRequest.IsRealPerson, !aRequest.IsRealPerson);

        return new VoiceCloneResponse
        {
            CloneId = vClone.CloneId,
            Blocked = false,
            ConsentVerified = aRequest.IsRealPerson,
            IsSynthetic = !aRequest.IsRealPerson,
            Status = "ConsentVerifiedModelPending",
            Message = aRequest.IsRealPerson
                ? "Consent verified — clone registered. Voice-model training runs once a TTS model is installed."
                : "Synthetic clone registered (ungated). Voice-model training runs once a TTS model is installed."
        };
    }
}
