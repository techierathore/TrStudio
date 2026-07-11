using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Avatar;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Providers;

/// <summary>
/// Local lip-sync avatar <see cref="IGenerationProvider"/> (FN-037/BRD-28), gated to
/// <see cref="ProviderKind.ComfyUi"/> so it backs the Fast tier's local avatar models
/// (MuseTalk / LatentSync / SadTalker / LivePortrait) while the resolver still matches exactly one
/// provider per model. GRACEFUL DEGRADE: with no local lip-sync model installed the job resolves to a
/// Failed status carrying the actionable "lip-sync model not installed" message plus a remediation
/// hint (REQ-FN-005 style) instead of crashing — unless the dev synthetic fallback is on, in which
/// case it emits a deterministic placeholder video asset so the pipeline is exercisable.
/// </summary>
public class AvatarProvider : IGenerationProvider
{
    private const int ProgressSteps = 8;

    private readonly AvatarOptions objOptions;
    private readonly IAssetStorage objAssetStorage;
    private readonly IAssetRepository objAssetRepository;
    private readonly ILogger<AvatarProvider> objLogger;
    private readonly ConcurrentDictionary<Guid, AvatarExecution> objExecutions = new();

    /// <summary>
    /// Initialises the provider.
    /// </summary>
    /// <param name="aOptions">Avatar options (installed-model flag, synthetic fallback).</param>
    /// <param name="aAssetStorage">Local object store receiving the output binary.</param>
    /// <param name="aAssetRepository">Asset metadata persistence.</param>
    /// <param name="aLogger">Structured logger.</param>
    public AvatarProvider(
        AvatarOptions aOptions,
        IAssetStorage aAssetStorage,
        IAssetRepository aAssetRepository,
        ILogger<AvatarProvider> aLogger)
    {
        objOptions = aOptions;
        objAssetStorage = aAssetStorage;
        objAssetRepository = aAssetRepository;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    // A ComfyUi-kind model is shared with ComfyUiProvider, so the LipSync task is the discriminator:
    // AvatarProvider owns the lip-sync avatar workflows (e.g. musetalk-fast), ComfyUiProvider owns the
    // general WorkflowRun/image/video models. Without this the resolver sees two claimants and throws.
    public bool Supports(GenerationTaskType aTask, RegistryModel aModel)
        => aModel.ProviderKind == ProviderKind.ComfyUi && aTask == GenerationTaskType.LipSync;

    /// <inheritdoc />
    public Task<Guid> SubmitAsync(GenerationJob aJob, CancellationToken aCancellationToken)
    {
        var vExecution = new AvatarExecution();
        objExecutions[aJob.JobId] = vExecution;

        if (!objOptions.LocalLipSyncInstalled && !objOptions.SyntheticFallback)
        {
            // Graceful degrade: no crash — the job fails with an actionable remediation (FN-037).
            vExecution.Status = new JobStatus(
                JobState.Failed, 0, null,
                AvatarOptions.LocalNotInstalledMessage,
                "Install a local lip-sync model under Admin > Models, or submit this avatar job at the High (RunPod) or Premium (HeyGen) tier.");
            objLogger.LogWarning("Avatar job {JobId} degraded: no local lip-sync model installed", aJob.JobId);
            return Task.FromResult(aJob.JobId);
        }

        _ = Task.Run(() => RunAsync(aJob, vExecution), CancellationToken.None);
        return Task.FromResult(aJob.JobId);
    }

    /// <inheritdoc />
    public Task<JobStatus> GetStatusAsync(Guid aJobId, CancellationToken aCancellationToken)
    {
        if (!objExecutions.TryGetValue(aJobId, out var vExecution))
        {
            throw new InvalidOperationException($"Job '{aJobId}' was not submitted to this provider instance.");
        }

        return Task.FromResult(vExecution.Status);
    }

    /// <inheritdoc />
    public Task<AssetReference> GetResultAsync(Guid aJobId, CancellationToken aCancellationToken)
    {
        if (!objExecutions.TryGetValue(aJobId, out var vExecution) || vExecution.Result is null)
        {
            throw new InvalidOperationException($"Job '{aJobId}' has not completed successfully.");
        }

        return Task.FromResult(vExecution.Result);
    }

    /// <inheritdoc />
    public Task<decimal> EstimateCostAsync(GenerationJob aJob, CancellationToken aCancellationToken)
        => Task.FromResult(0m); // Local lip-sync is free; the orchestrator prices via the registry cost block.

    private async Task RunAsync(GenerationJob aJob, AvatarExecution aExecution)
    {
        try
        {
            var vStepDelay = TimeSpan.FromSeconds(2d / ProgressSteps);
            for (var vStep = 1; vStep < ProgressSteps; vStep++)
            {
                await Task.Delay(vStepDelay).ConfigureAwait(false);
                var vProgress = vStep * (100 / ProgressSteps);
                var vRemaining = TimeSpan.FromSeconds(vStepDelay.TotalSeconds * (ProgressSteps - vStep));
                aExecution.Status = new JobStatus(JobState.Running, vProgress, vRemaining, null, null);
            }

            var vResult = await PersistAssetAsync(aJob).ConfigureAwait(false);
            aExecution.Result = vResult;
            aExecution.Status = new JobStatus(JobState.Succeeded, 100, TimeSpan.Zero, null, null);
        }
        catch (Exception vException)
        {
            objLogger.LogError(vException, "AvatarProvider execution failed for job {JobId}", aJob.JobId);
            aExecution.Status = new JobStatus(JobState.Failed, aExecution.Status.Progress, null, vException.Message, null);
        }
    }

    private async Task<AssetReference> PersistAssetAsync(GenerationJob aJob)
    {
        var vBytes = SyntheticVideo.BuildPlaceholderMp4();
        var vUserFolder = SanitizeForPath(aJob.UserId);
        var vStoragePath = await objAssetStorage
            .SaveAsync(Path.Combine(vUserFolder, "avatar", $"{aJob.JobId}.mp4"), vBytes)
            .ConfigureAwait(false);

        var vAsset = new GeneratedAsset
        {
            AssetId = Guid.NewGuid(),
            JobId = aJob.JobId,
            UserId = aJob.UserId,
            ProjectId = aJob.ProjectId,
            Kind = "video",
            StoragePath = vStoragePath,
            ThumbnailPath = null,
            Width = null,
            Height = null,
            SeedUsed = aJob.SeedUsed,
            ParamsJson = aJob.ParamsJson,
            ModelId = aJob.ModelId,
            ProvenanceJson = JsonSerializer.Serialize(new
            {
                Generator = "TrStudio AvatarProvider (synthetic-dev)",
                ModelId = aJob.ModelId,
                JobId = aJob.JobId,
                CreatedUtc = DateTime.UtcNow
            }),
            CreatedUtc = DateTime.UtcNow
        };

        await objAssetRepository.InsertAsync(vAsset).ConfigureAwait(false);
        return new AssetReference(vAsset.AssetId, vAsset.StoragePath, vAsset.Kind);
    }

    private static string SanitizeForPath(string aUserId)
    {
        var vInvalid = Path.GetInvalidFileNameChars();
        var vClean = new string(aUserId.Select(aChar => vInvalid.Contains(aChar) ? '_' : aChar).ToArray());
        return string.IsNullOrWhiteSpace(vClean) ? "unknown" : vClean;
    }

    /// <summary>Mutable in-memory execution record for one submitted avatar job.</summary>
    private sealed class AvatarExecution
    {
        public JobStatus Status { get; set; } = new(JobState.Running, 0, null, null, null);
        public AssetReference? Result { get; set; }
    }
}
