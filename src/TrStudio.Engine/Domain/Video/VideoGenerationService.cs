using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Cost;
using TrStudio.Engine.Domain.Jobs;
using TrStudio.Engine.Domain.Providers;
using TrStudio.Engine.Domain.Registry;
using TrStudio.Engine.Domain.RunPod;
using TrStudio.Engine.Domain.Safety;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Domain.Video;

/// <summary>
/// Default <see cref="IVideoGenerationService"/> (FN-051/052). Resolves the requested mode to a task,
/// validates the creator controls (duration/motion/aspect) and the FN-052 conditioning references
/// (first/last-frame + reference image, each owner-scoped), tier-routes to a registry model whose
/// provider differs per tier (Fast → local, High → RunPod, Premium → closed aggregator), and submits
/// through <see cref="IJobOrchestrator"/> so the non-disableable safety pre-check runs and the spend
/// cap is honoured. The prompt is carried in the job params so the safety classifier evaluates it.
/// </summary>
public class VideoGenerationService : IVideoGenerationService
{
    private readonly IVideoTierRouter objTierRouter;
    private readonly IModelCatalogService objModelCatalog;
    private readonly IProviderResolver objProviderResolver;
    private readonly IJobOrchestrator objOrchestrator;
    private readonly IAssetRepository objAssetRepository;
    private readonly VideoOptions objVideoOptions;
    private readonly RunPodOptions objRunPodOptions;
    private readonly ClosedApiOptions objClosedApiOptions;
    private readonly ILogger<VideoGenerationService> objLogger;

    /// <summary>
    /// Initialises the service.
    /// </summary>
    /// <param name="aTierRouter">Tier → registry-model routing (FN-051).</param>
    /// <param name="aModelCatalog">Creator-visible model catalog (resolves the routed model).</param>
    /// <param name="aProviderResolver">Model → provider resolution (tier-routing evidence).</param>
    /// <param name="aOrchestrator">The job spine (safety pre-check + spend cap + durable enqueue).</param>
    /// <param name="aAssetRepository">Owner-scoped asset lookup for conditioning validation (BRD-81).</param>
    /// <param name="aVideoOptions">Video options (local model install flag for degrade signalling).</param>
    /// <param name="aRunPodOptions">RunPod options (High-tier config for degrade signalling).</param>
    /// <param name="aClosedApiOptions">Closed-API options (Premium-tier config for degrade signalling).</param>
    /// <param name="aLogger">Structured logger.</param>
    public VideoGenerationService(
        IVideoTierRouter aTierRouter,
        IModelCatalogService aModelCatalog,
        IProviderResolver aProviderResolver,
        IJobOrchestrator aOrchestrator,
        IAssetRepository aAssetRepository,
        VideoOptions aVideoOptions,
        RunPodOptions aRunPodOptions,
        ClosedApiOptions aClosedApiOptions,
        ILogger<VideoGenerationService> aLogger)
    {
        objTierRouter = aTierRouter;
        objModelCatalog = aModelCatalog;
        objProviderResolver = aProviderResolver;
        objOrchestrator = aOrchestrator;
        objAssetRepository = aAssetRepository;
        objVideoOptions = aVideoOptions;
        objRunPodOptions = aRunPodOptions;
        objClosedApiOptions = aClosedApiOptions;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<VideoGenerateResponse> SubmitAsync(VideoGenerateRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vMode = (aRequest.Mode ?? string.Empty).Trim().ToLowerInvariant();
        var vTask = vMode switch
        {
            "i2v" or "image-to-video" or "imagetovideo" => GenerationTaskType.ImageToVideo,
            "t2v" or "text-to-video" or "texttovideo" => GenerationTaskType.TextToVideo,
            _ => (GenerationTaskType?)null
        };

        var vResponse = new VideoGenerateResponse { Tier = aRequest.Tier };

        if (vTask is null)
        {
            return Error(vResponse, "Mode must be 'i2v' (image-to-video) or 't2v' (text-to-video).");
        }

        vResponse.Mode = vTask == GenerationTaskType.ImageToVideo ? "i2v" : "t2v";

        // ---- Control validation (duration / motion / aspect). ----
        var vAspect = VideoFormat.Normalize(aRequest.AspectRatio);
        if (vAspect is null)
        {
            return Error(vResponse, $"AspectRatio must be one of: {string.Join(", ", VideoFormat.Supported)}.");
        }

        if (vTask == GenerationTaskType.TextToVideo && string.IsNullOrWhiteSpace(aRequest.Prompt))
        {
            return Error(vResponse, "A prompt is required for text-to-video (T2V).");
        }

        if (vTask == GenerationTaskType.ImageToVideo && (aRequest.SourceImageAssetId is null || aRequest.SourceImageAssetId == Guid.Empty))
        {
            return Error(vResponse, "A sourceImageAssetId is required for image-to-video (I2V).");
        }

        // ---- FN-052 conditioning + I2V source: every asset reference is owner-scoped (BRD-81). ----
        var vConditioning = new (Guid? Id, string Name)[]
        {
            (aRequest.SourceImageAssetId, "sourceImageAssetId"),
            (aRequest.FirstFrameAssetId, "firstFrameAssetId"),
            (aRequest.LastFrameAssetId, "lastFrameAssetId"),
            (aRequest.ReferenceAssetId, "referenceAssetId")
        };

        foreach (var (vId, vName) in vConditioning)
        {
            if (vId is null || vId == Guid.Empty)
            {
                continue;
            }

            var vAsset = await objAssetRepository.GetAsync(vId.Value, aUserId, aCancellationToken).ConfigureAwait(false);
            if (vAsset is null)
            {
                objLogger.LogWarning("Video job rejected: {Param} {AssetId} not found for user {UserId}", vName, vId, aUserId);
                return Error(vResponse, $"The {vName} asset was not found or is not owned by the caller.");
            }
        }

        // ---- FN-051: tier-route to a registry model whose provider differs per tier. ----
        var vModelId = objTierRouter.ResolveModelId(vTask.Value, aRequest.Tier);
        vResponse.ResolvedModelId = vModelId;

        var vModel = await objModelCatalog.GetVisibleAsync(vModelId, aCancellationToken).ConfigureAwait(false);
        if (vModel is null)
        {
            return Error(vResponse, $"The video model '{vModelId}' backing the {aRequest.Tier} tier is not registered or is disabled.");
        }

        vResponse.ResolvedProviderKind = vModel.ProviderKind;
        vResponse.ResolvedProvider = objProviderResolver.Resolve(vModel).GetType().Name;
        vResponse.WillDegrade = TierWillDegrade(aRequest.Tier);

        var vParamsJson = BuildParams(aRequest, vTask.Value, vAspect);
        var vSubmit = new JobSubmitRequest
        {
            ModelId = vModelId,
            Task = vTask.Value,
            Tier = aRequest.Tier,
            ParamsJson = vParamsJson,
            ProjectId = aRequest.ProjectId,
            Seed = aRequest.Seed
        };

        try
        {
            var vJob = await objOrchestrator.SubmitAsync(vSubmit, aUserId, aCancellationToken).ConfigureAwait(false);
            vResponse.JobId = vJob.JobId;
            vResponse.CostEstimate = vJob.CostEstimate;
            vResponse.Warning = vJob.Warning;
            vResponse.Status = "queued";
            vResponse.Message = vResponse.WillDegrade
                ? $"Queued on the {aRequest.Tier} tier ({vResponse.ResolvedProvider}); the provider is not configured, so the job will degrade cleanly with an actionable message."
                : $"Queued on the {aRequest.Tier} tier ({vResponse.ResolvedProvider}).";
            return vResponse;
        }
        catch (SafetyBlockedException vException)
        {
            // FN-063 / SAFE-01..06: the non-disableable floors refused this video job.
            vResponse.Status = "blocked";
            vResponse.Message = vException.Message;
            objLogger.LogWarning("Video job for model {ModelId} BLOCKED by floor {Floor}", vModelId, vException.Floor);
            return vResponse;
        }
        catch (SpendCapExceededException vException)
        {
            vResponse.Status = "error";
            vResponse.Message = vException.Message;
            objLogger.LogWarning("Video job for model {ModelId} blocked at submit by the spend cap", vModelId);
            return vResponse;
        }
    }

    /// <summary>Merges the request controls + conditioning into the job params (prompt carried for the safety classifier).</summary>
    private static string BuildParams(VideoGenerateRequest aRequest, GenerationTaskType aTask, string aAspect)
    {
        var vParams = new Dictionary<string, object?>
        {
            ["mode"] = aTask == GenerationTaskType.ImageToVideo ? "i2v" : "t2v",
            ["prompt"] = aRequest.Prompt ?? string.Empty, // Text signal for the safety classifier (FN-063).
            ["durationSeconds"] = VideoFormat.ClampDuration(aRequest.DurationSeconds),
            ["motion"] = VideoFormat.ClampMotion(aRequest.Motion),
            ["aspectRatio"] = aAspect
        };

        AddAsset(vParams, "sourceImageAssetId", aRequest.SourceImageAssetId);
        AddAsset(vParams, "firstFrameAssetId", aRequest.FirstFrameAssetId);
        AddAsset(vParams, "lastFrameAssetId", aRequest.LastFrameAssetId);
        AddAsset(vParams, "referenceAssetId", aRequest.ReferenceAssetId);

        return JsonSerializer.Serialize(vParams);
    }

    private static void AddAsset(Dictionary<string, object?> aParams, string aKey, Guid? aId)
    {
        if (aId is { } vId && vId != Guid.Empty)
        {
            aParams[aKey] = vId.ToString();
        }
    }

    private bool TierWillDegrade(GenerationTier aTier) => aTier switch
    {
        GenerationTier.Fast => !objVideoOptions.LocalVideoModelInstalled,
        GenerationTier.High => !objRunPodOptions.IsConfigured,
        GenerationTier.Premium => !objClosedApiOptions.IsConfigured,
        _ => true
    };

    private static VideoGenerateResponse Error(VideoGenerateResponse aResponse, string aMessage)
    {
        aResponse.Status = "error";
        aResponse.Message = aMessage;
        return aResponse;
    }
}
