using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrStudio.Models.Contracts;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Voice;

/// <summary>
/// Default <see cref="IAudioAvatarHandoff"/> (FN-035/BRD-26). Turns a completed voice asset plus its
/// timing metadata into the structural job-input contract the Phase-5 avatar pipeline consumes. The
/// avatar pipeline itself is out of scope here — this only produces and validates the handoff.
/// </summary>
public class AudioAvatarHandoff : IAudioAvatarHandoff
{
    private readonly IAssetRepository objAssetRepository;
    private readonly ILogger<AudioAvatarHandoff> objLogger;

    /// <summary>
    /// Initialises the handoff builder.
    /// </summary>
    /// <param name="aAssetRepository">Owner-filtered asset lookup for validation.</param>
    /// <param name="aLogger">Structured logger.</param>
    public AudioAvatarHandoff(IAssetRepository aAssetRepository, ILogger<AudioAvatarHandoff> aLogger)
    {
        objAssetRepository = aAssetRepository;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<AvatarHandoffResponse> BuildHandoffAsync(AvatarHandoffRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        // Owner-filtered lookup so a handoff can never be built from another user's asset (BRD-81).
        var vAsset = await objAssetRepository.GetAsync(aRequest.AssetId, aUserId, aCancellationToken).ConfigureAwait(false);

        var vResponse = new AvatarHandoffResponse
        {
            AssetId = aRequest.AssetId,
            Language = string.IsNullOrWhiteSpace(aRequest.Language) ? "en" : aRequest.Language,
            SampleRateHz = SyntheticAudio.SampleRateHz,
            AudioStoragePath = aRequest.AudioStoragePath,
            DurationSeconds = aRequest.DurationSeconds
        };

        if (vAsset is null)
        {
            vResponse.Status = "pending";
            vResponse.Message = "Voice asset not found or not owned by the caller — cannot build the avatar handoff.";
            return vResponse;
        }

        vResponse.AudioStoragePath = string.IsNullOrWhiteSpace(aRequest.AudioStoragePath) ? vAsset.StoragePath : aRequest.AudioStoragePath;
        vResponse.Status = "ready-for-avatar";

        // The avatar-pipeline job input: the audio, its timing metadata, and the alignment stage the
        // Phase-5 lip-sync/avatar worker will run. Serialized so it can be enqueued verbatim later.
        var vJobInput = new
        {
            SourceAssetId = vAsset.AssetId,
            AudioStoragePath = vResponse.AudioStoragePath,
            DurationSeconds = vResponse.DurationSeconds,
            SampleRateHz = vResponse.SampleRateHz,
            Language = vResponse.Language,
            Alignment = new { Stage = "phoneme-to-viseme", Provider = "avatar-pipeline", Phase = 5 },
            CreatedUtc = DateTime.UtcNow
        };
        vResponse.AvatarJobInputJson = JsonSerializer.Serialize(vJobInput);
        vResponse.Message = "Audio→avatar handoff prepared. The Phase-5 avatar pipeline consumes AvatarJobInputJson.";

        objLogger.LogInformation("Prepared avatar handoff for asset {AssetId} ({Duration}s)", vAsset.AssetId, vResponse.DurationSeconds);
        return vResponse;
    }
}
