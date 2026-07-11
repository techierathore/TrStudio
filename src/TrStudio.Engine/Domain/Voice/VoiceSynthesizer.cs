using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Voice;

/// <summary>
/// Default <see cref="IVoiceSynthesizer"/> (FN-031/033/034/BRD-22..25). Resolves the preset voice,
/// routes to the per-language model (FN-033), reconciles the requested emotion/style/speed against
/// the voice's capability descriptor (FN-034), and — because no real model is installed — either
/// degrades with an actionable "voice model not installed" message or emits a deterministic silent
/// WAV asset (dev synthetic path) carrying realistic timing metadata for the avatar handoff (FN-035).
/// </summary>
public class VoiceSynthesizer : IVoiceSynthesizer
{
    private readonly IPresetVoiceCatalog objCatalog;
    private readonly IVoiceModelRouter objRouter;
    private readonly VoiceOptions objOptions;
    private readonly IAssetStorage objAssetStorage;
    private readonly IAssetRepository objAssetRepository;
    private readonly ILogger<VoiceSynthesizer> objLogger;

    /// <summary>
    /// Initialises the synthesizer.
    /// </summary>
    /// <param name="aCatalog">The preset-voice library.</param>
    /// <param name="aRouter">Per-language model router.</param>
    /// <param name="aOptions">Voice options (installed models, synthetic fallback).</param>
    /// <param name="aAssetStorage">Local object store receiving the audio binary.</param>
    /// <param name="aAssetRepository">Asset metadata persistence.</param>
    /// <param name="aLogger">Structured logger.</param>
    public VoiceSynthesizer(
        IPresetVoiceCatalog aCatalog,
        IVoiceModelRouter aRouter,
        VoiceOptions aOptions,
        IAssetStorage aAssetStorage,
        IAssetRepository aAssetRepository,
        ILogger<VoiceSynthesizer> aLogger)
    {
        objCatalog = aCatalog;
        objRouter = aRouter;
        objOptions = aOptions;
        objAssetStorage = aAssetStorage;
        objAssetRepository = aAssetRepository;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<VoiceSynthesizeResponse> SynthesizeAsync(VoiceSynthesizeRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vVoice = objCatalog.Get(aRequest.VoiceId);
        var vLanguage = vVoice?.Language ?? aRequest.Language;
        var vRoute = objRouter.Route(vLanguage);

        var vResponse = new VoiceSynthesizeResponse
        {
            VoiceId = aRequest.VoiceId,
            Language = vLanguage,
            ResolvedModel = vRoute.Model
        };

        if (vVoice is null)
        {
            vResponse.Degraded = true;
            vResponse.Mode = "model-not-installed";
            vResponse.Message =
                $"Unknown voice '{aRequest.VoiceId}'. Call GET {ApiVoiceListHint} to list preset voices.";
            return vResponse;
        }

        // FN-034 — reconcile requested controls against the voice's capability descriptor.
        ReconcileControls(aRequest, vVoice, vResponse);

        var vModelReady = objOptions.SynthesisEnabled && vRoute.Installed;
        if (vModelReady)
        {
            // A real model backend would run here. None is installed in this phase; if the flags are
            // ever set without a backend we still fall through to the graceful paths below.
            objLogger.LogInformation(
                "Voice model {Model} reported ready for {Language}; real synthesis backend is wired in a later phase", vRoute.Model, vLanguage);
        }

        if (!objOptions.SyntheticFallback)
        {
            // Pure graceful degrade — no audio, actionable remediation (REQ-FN-005 style).
            vResponse.Degraded = true;
            vResponse.Mode = "model-not-installed";
            vResponse.Message = VoiceOptions.ModelNotInstalledMessage;
            objLogger.LogWarning("Voice synthesis degraded: model {Model} not installed for {Language}", vRoute.Model, vLanguage);
            return vResponse;
        }

        // Deterministic synthetic path: emit a silent WAV sized to the text so the pipeline and its
        // timing metadata (FN-035) are exercised without a real model.
        var vDuration = SyntheticAudio.EstimateDurationSeconds(aRequest.Text, aRequest.Speed);
        var vBytes = SyntheticAudio.BuildSilentWav(vDuration);
        var vAsset = await PersistAudioAsync(aRequest, aUserId, vRoute.Model, vBytes, aCancellationToken).ConfigureAwait(false);

        vResponse.Degraded = true;
        vResponse.Mode = "synthetic-dev";
        vResponse.Message =
            "Voice model not installed — returned deterministic placeholder audio (dev synthetic path). " +
            VoiceOptions.ModelNotInstalledMessage;
        vResponse.AssetId = vAsset.AssetId;
        vResponse.StoragePath = vAsset.StoragePath;
        vResponse.DurationSeconds = vDuration;
        return vResponse;
    }

    private const string ApiVoiceListHint = "/api/v1/voice/preset-voices";

    private static void ReconcileControls(VoiceSynthesizeRequest aRequest, PresetVoice aVoice, VoiceSynthesizeResponse aResponse)
    {
        if (!string.IsNullOrWhiteSpace(aRequest.Emotion))
        {
            if (aVoice.SupportsEmotion && aVoice.Emotions.Contains(aRequest.Emotion, StringComparer.OrdinalIgnoreCase))
            {
                aResponse.AppliedControls.Add($"emotion:{aRequest.Emotion}");
            }
            else
            {
                aResponse.UnsupportedControls.Add($"emotion:{aRequest.Emotion}");
            }
        }

        if (!string.IsNullOrWhiteSpace(aRequest.Style))
        {
            if (aVoice.SupportsStyle)
            {
                aResponse.AppliedControls.Add($"style:{aRequest.Style}");
            }
            else
            {
                aResponse.UnsupportedControls.Add($"style:{aRequest.Style}");
            }
        }

        // Speed 1.0 means "no change"; only report it as a control when the caller adjusted it.
        if (Math.Abs(aRequest.Speed - 1.0d) > 0.0001d)
        {
            if (aVoice.SupportsSpeed)
            {
                aResponse.AppliedControls.Add($"speed:{aRequest.Speed:0.##}");
            }
            else
            {
                aResponse.UnsupportedControls.Add($"speed:{aRequest.Speed:0.##}");
            }
        }
    }

    private async Task<GeneratedAsset> PersistAudioAsync(
        VoiceSynthesizeRequest aRequest,
        string aUserId,
        string aModel,
        byte[] aBytes,
        CancellationToken aCancellationToken)
    {
        var vAssetId = Guid.NewGuid();
        var vUserFolder = SanitizeForPath(aUserId);
        var vStoragePath = await objAssetStorage
            .SaveAsync(Path.Combine(vUserFolder, "voice", $"{vAssetId}.wav"), aBytes, aCancellationToken)
            .ConfigureAwait(false);

        var vAsset = new GeneratedAsset
        {
            AssetId = vAssetId,
            JobId = vAssetId,
            UserId = aUserId,
            ProjectId = aRequest.ProjectId,
            Kind = "audio",
            StoragePath = vStoragePath,
            ThumbnailPath = null,
            Width = null,
            Height = null,
            SeedUsed = null,
            ParamsJson = JsonSerializer.Serialize(new { aRequest.VoiceId, aRequest.Language, aRequest.Emotion, aRequest.Style, aRequest.Speed }),
            ModelId = aModel,
            ProvenanceJson = JsonSerializer.Serialize(new
            {
                Generator = "TrStudio VoiceSynthesizer (synthetic-dev)",
                Model = aModel,
                Language = aRequest.Language,
                CreatedUtc = DateTime.UtcNow
            }),
            CreatedUtc = DateTime.UtcNow
        };

        await objAssetRepository.InsertAsync(vAsset, aCancellationToken).ConfigureAwait(false);
        return vAsset;
    }

    private static string SanitizeForPath(string aUserId)
    {
        var vInvalid = Path.GetInvalidFileNameChars();
        var vClean = new string(aUserId.Select(aChar => vInvalid.Contains(aChar) ? '_' : aChar).ToArray());
        return string.IsNullOrWhiteSpace(vClean) ? "unknown" : vClean;
    }
}
