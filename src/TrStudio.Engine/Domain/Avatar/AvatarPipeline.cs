using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Jobs;
using TrStudio.Engine.Domain.Providers;
using TrStudio.Engine.Domain.Registry;
using TrStudio.Engine.Domain.RunPod;
using TrStudio.Engine.Domain.Safety;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Avatar;

/// <summary>
/// Default <see cref="IAvatarPipeline"/> (FN-037/038/039/040 — the flagship). A talking-avatar job is
/// routed by <see cref="IAvatarTierRouter"/> to a per-tier registry model and submitted through the
/// proven job spine (<see cref="IJobOrchestrator"/>), so the single tier control swaps the provider
/// underneath (FN-039) AND the non-disableable safety floors run on every provider (FN-063). The
/// character's locked identity (seed + reference set) and its realism/real-person safety signals are
/// merged into the job params so the persona reproduces and the safety pipeline can act on it. The
/// script→video chain (FN-038) sequences TTS → lip-sync → captions (FN-040), each degrading
/// independently on a missing model without breaking the chain.
/// </summary>
public class AvatarPipeline : IAvatarPipeline
{
    private readonly ICharacterRepository objCharacterRepository;
    private readonly IAvatarTierRouter objTierRouter;
    private readonly IModelCatalogService objModelCatalog;
    private readonly IProviderResolver objProviderResolver;
    private readonly IJobOrchestrator objOrchestrator;
    private readonly IVoiceSynthesizer objVoiceSynthesizer;
    private readonly AvatarOptions objAvatarOptions;
    private readonly RunPodOptions objRunPodOptions;
    private readonly ILogger<AvatarPipeline> objLogger;

    /// <summary>
    /// Initialises the pipeline.
    /// </summary>
    /// <param name="aCharacterRepository">Owner-filtered character lookup (BRD-81).</param>
    /// <param name="aTierRouter">Tier → registry-model routing (FN-039).</param>
    /// <param name="aModelCatalog">Creator-visible model catalog (resolves the routed model).</param>
    /// <param name="aProviderResolver">Model → provider resolution (FN-039 evidence).</param>
    /// <param name="aOrchestrator">The job spine (safety pre-check + durable enqueue).</param>
    /// <param name="aVoiceSynthesizer">The Voice synthesizer reused for the script→video TTS stage (FN-035/038).</param>
    /// <param name="aAvatarOptions">Avatar options (local lip-sync + HeyGen config for degrade signalling).</param>
    /// <param name="aRunPodOptions">RunPod options (High-tier config for degrade signalling).</param>
    /// <param name="aLogger">Structured logger.</param>
    public AvatarPipeline(
        ICharacterRepository aCharacterRepository,
        IAvatarTierRouter aTierRouter,
        IModelCatalogService aModelCatalog,
        IProviderResolver aProviderResolver,
        IJobOrchestrator aOrchestrator,
        IVoiceSynthesizer aVoiceSynthesizer,
        AvatarOptions aAvatarOptions,
        RunPodOptions aRunPodOptions,
        ILogger<AvatarPipeline> aLogger)
    {
        objCharacterRepository = aCharacterRepository;
        objTierRouter = aTierRouter;
        objModelCatalog = aModelCatalog;
        objProviderResolver = aProviderResolver;
        objOrchestrator = aOrchestrator;
        objVoiceSynthesizer = aVoiceSynthesizer;
        objAvatarOptions = aAvatarOptions;
        objRunPodOptions = aRunPodOptions;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<AvatarTalkingResponse?> SubmitTalkingAvatarAsync(AvatarTalkingRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vCharacter = await objCharacterRepository.GetAsync(aRequest.CharacterId, aUserId, aCancellationToken).ConfigureAwait(false);
        if (vCharacter is null)
        {
            return null;
        }

        // FN-039: the SAME job spec routes to a different registry model — and therefore a different
        // provider — per tier. Resolve the model + its provider so the caller can prove the swap.
        var vModelId = objTierRouter.ResolveModelId(aRequest.Tier);
        var vModel = await objModelCatalog.GetVisibleAsync(vModelId, aCancellationToken).ConfigureAwait(false);

        var vResponse = new AvatarTalkingResponse
        {
            CharacterId = vCharacter.CharacterId,
            Tier = aRequest.Tier,
            ResolvedModelId = vModelId
        };

        if (vModel is null)
        {
            vResponse.Status = "error";
            vResponse.Message = $"The lip-sync model '{vModelId}' backing the {aRequest.Tier} tier is not registered or is disabled.";
            objLogger.LogWarning("Avatar tier {Tier} model {ModelId} unavailable for user {UserId}", aRequest.Tier, vModelId, aUserId);
            return vResponse;
        }

        vResponse.ResolvedProviderKind = vModel.ProviderKind;
        vResponse.ResolvedProvider = objProviderResolver.Resolve(vModel).GetType().Name;
        vResponse.WillDegrade = TierWillDegrade(aRequest.Tier);

        var vParamsJson = BuildAvatarParams(vCharacter, aRequest.Script, aRequest.AudioAssetId, aRequest.Language);
        var vSubmit = new JobSubmitRequest
        {
            ModelId = vModelId,
            Task = GenerationTaskType.LipSync,
            Tier = aRequest.Tier,
            ParamsJson = vParamsJson,
            ProjectId = aRequest.ProjectId,
            Seed = vCharacter.Seed // Reuse the character's LOCKED identity seed for reproducibility (FN-036).
        };

        try
        {
            var vJob = await objOrchestrator.SubmitAsync(vSubmit, aUserId, aCancellationToken).ConfigureAwait(false);
            vResponse.JobId = vJob.JobId;
            vResponse.Status = "queued";
            vResponse.Message = vResponse.WillDegrade
                ? $"Queued on the {aRequest.Tier} tier ({vResponse.ResolvedProvider}); the provider is not configured, so the job will degrade cleanly with an actionable message."
                : $"Queued on the {aRequest.Tier} tier ({vResponse.ResolvedProvider}).";
            return vResponse;
        }
        catch (SafetyBlockedException vException)
        {
            // FN-063 / SAFE-01..06: the non-disableable floors refused this persona's avatar job.
            vResponse.Status = "blocked";
            vResponse.Message = vException.Message;
            objLogger.LogWarning("Avatar job for character {CharacterId} BLOCKED by floor {Floor}", vCharacter.CharacterId, vException.Floor);
            return vResponse;
        }
    }

    /// <inheritdoc />
    public async Task<ScriptToVideoResponse?> RunScriptToVideoAsync(ScriptToVideoRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vCharacter = await objCharacterRepository.GetAsync(aRequest.CharacterId, aUserId, aCancellationToken).ConfigureAwait(false);
        if (vCharacter is null)
        {
            return null;
        }

        var vResponse = new ScriptToVideoResponse
        {
            CharacterId = vCharacter.CharacterId,
            Tier = aRequest.Tier,
            ResolvedModelId = objTierRouter.ResolveModelId(aRequest.Tier)
        };

        // ---- Stage 1: TTS (reuse the built Voice synthesizer, FN-031/035). ----
        var vTts = await objVoiceSynthesizer.SynthesizeAsync(new VoiceSynthesizeRequest
        {
            Text = aRequest.Script,
            VoiceId = aRequest.VoiceId,
            Language = aRequest.Language,
            ProjectId = aRequest.ProjectId
        }, aUserId, aCancellationToken).ConfigureAwait(false);

        vResponse.AudioAssetId = vTts.AssetId;
        vResponse.Stages.Add(new ScriptToVideoResponse.PipelineStage
        {
            Name = "tts",
            Status = vTts.Degraded ? "degraded" : "ok",
            Detail = vTts.Message ?? $"Synthesized via {vTts.ResolvedModel}."
        });
        if (vTts.Degraded)
        {
            vResponse.Degraded = true;
        }

        // ---- Stage 2: lip-sync (FN-037), tier-routed (FN-039) through the job spine. ----
        var vLipSync = await SubmitTalkingAvatarAsync(new AvatarTalkingRequest
        {
            CharacterId = vCharacter.CharacterId,
            Script = aRequest.Script,
            AudioAssetId = vTts.AssetId,
            Language = aRequest.Language,
            Tier = aRequest.Tier,
            ProjectId = aRequest.ProjectId
        }, aUserId, aCancellationToken).ConfigureAwait(false);

        if (vLipSync is not null)
        {
            vResponse.ResolvedProviderKind = vLipSync.ResolvedProviderKind;
            vResponse.LipSyncJobId = vLipSync.Status == "queued" ? vLipSync.JobId : null;
            vResponse.Stages.Add(new ScriptToVideoResponse.PipelineStage
            {
                Name = "lip-sync",
                Status = vLipSync.Status == "queued" ? (vLipSync.WillDegrade ? "degraded" : "queued") : vLipSync.Status,
                Detail = vLipSync.Message
            });
            if (vLipSync.Status == "blocked" || vLipSync.WillDegrade)
            {
                vResponse.Degraded = true;
            }
        }

        // ---- Stage 3: captions/subtitles (FN-040). ----
        if (aRequest.IncludeCaptions)
        {
            var vSubtitles = AvatarCaptionBuilder.Build(new SubtitleRequest
            {
                Script = aRequest.Script,
                Language = aRequest.Language,
                DurationSeconds = vTts.DurationSeconds > 0 ? vTts.DurationSeconds : (double?)null,
                Format = "srt"
            });
            vResponse.Captions = vSubtitles.Content;
            vResponse.Stages.Add(new ScriptToVideoResponse.PipelineStage
            {
                Name = "captions",
                Status = vSubtitles.CueCount > 0 ? "ok" : "skipped",
                Detail = $"{vSubtitles.CueCount} SRT cue(s) over {vSubtitles.DurationSeconds:0.###}s."
            });
        }

        vResponse.Message = vResponse.Degraded
            ? "Script→video pipeline ran end-to-end; one or more stages degraded on a missing model/key (chain and contract intact)."
            : "Script→video pipeline queued end-to-end.";
        return vResponse;
    }

    /// <inheritdoc />
    public SubtitleResponse GenerateSubtitles(SubtitleRequest aRequest) => AvatarCaptionBuilder.Build(aRequest);

    /// <summary>
    /// Merges the character's LOCKED identity and its realism/real-person safety signals into the
    /// lip-sync job params, so the persona reproduces (FN-036) and the safety pipeline can act on the
    /// realism flag → NSFW-scope / real-person floors (FN-063).
    /// </summary>
    private static string BuildAvatarParams(Character aCharacter, string? aScript, Guid? aAudioAssetId, string aLanguage)
    {
        var vParams = new Dictionary<string, object?>
        {
            ["characterId"] = aCharacter.CharacterId.ToString(),
            ["script"] = aScript ?? string.Empty,
            ["prompt"] = aScript ?? aCharacter.Name, // Text signal for the safety classifier.
            ["language"] = string.IsNullOrWhiteSpace(aLanguage) ? "en" : aLanguage,
            ["seed"] = aCharacter.Seed,
            ["loraRef"] = aCharacter.LoraRef,
            ["identityEmbedding"] = aCharacter.IdentityEmbedding,
            // Safety signals derived from the character (FN-063 integration).
            ["isRealPerson"] = aCharacter.IsRealPerson,
            ["photorealOfRealPerson"] = aCharacter.PhotorealOfRealPerson,
            ["depictsMinor"] = aCharacter.DepictsMinor,
            ["synthetic"] = !aCharacter.IsRealPerson,
            ["adult"] = aCharacter.IsAdult
        };

        if (aAudioAssetId is { } vAudio && vAudio != Guid.Empty)
        {
            vParams["audioAssetId"] = vAudio.ToString();
        }

        if (aCharacter.IsRealPerson)
        {
            // A real-person persona carries a subject reference so SAFE-03's consent gate can act
            // (it blocks without recorded consent), rather than failing closed on a missing reference.
            vParams["subjectReference"] = string.IsNullOrWhiteSpace(aCharacter.IdentityEmbedding)
                ? aCharacter.Name
                : aCharacter.IdentityEmbedding;
        }

        return JsonSerializer.Serialize(vParams);
    }

    private bool TierWillDegrade(GenerationTier aTier) => aTier switch
    {
        GenerationTier.Fast => !objAvatarOptions.LocalLipSyncInstalled && !objAvatarOptions.SyntheticFallback,
        GenerationTier.High => !objRunPodOptions.IsConfigured,
        GenerationTier.Premium => !objAvatarOptions.HeyGenConfigured,
        _ => true
    };
}
