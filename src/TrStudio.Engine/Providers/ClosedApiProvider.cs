using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Avatar;
using TrStudio.Engine.Domain.Video;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Providers;

/// <summary>
/// Closed-API aggregator <see cref="IGenerationProvider"/> (FN-051/BRD-42), gated to
/// <see cref="ProviderKind.ClosedApi"/> so it backs the Premium video tier (I2V + T2V) while the
/// resolver still matches exactly one provider per model. It adapts a hosted video-model aggregator
/// (fal.ai / Replicate / WaveSpeed shape): submit → poll → download. GRACEFUL DEGRADE: with no
/// <c>ClosedApi:ApiKey</c> (the EMPTY default) a submitted job resolves to a Failed status carrying
/// the actionable "aggregator not configured" message plus a remediation hint (REQ-FN-005 style)
/// instead of crashing; the live aggregator call is deferred until a real key is supplied, at which
/// point the configured path emits a deterministic placeholder MP4 asset in this phase.
/// </summary>
public class ClosedApiProvider : IGenerationProvider
{
    private const int ProgressSteps = 6;

    private readonly ClosedApiOptions objOptions;
    private readonly IAssetStorage objAssetStorage;
    private readonly IAssetRepository objAssetRepository;
    private readonly ILogger<ClosedApiProvider> objLogger;
    private readonly ConcurrentDictionary<Guid, ClosedApiExecution> objExecutions = new();

    /// <summary>
    /// Initialises the provider.
    /// </summary>
    /// <param name="aOptions">Closed-API options carrying the aggregator API key (empty by default).</param>
    /// <param name="aAssetStorage">Local object store receiving the output binary.</param>
    /// <param name="aAssetRepository">Asset metadata persistence.</param>
    /// <param name="aLogger">Structured logger (never logs the API key).</param>
    public ClosedApiProvider(
        ClosedApiOptions aOptions,
        IAssetStorage aAssetStorage,
        IAssetRepository aAssetRepository,
        ILogger<ClosedApiProvider> aLogger)
    {
        objOptions = aOptions;
        objAssetStorage = aAssetStorage;
        objAssetRepository = aAssetRepository;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public bool Supports(GenerationTaskType aTask, RegistryModel aModel)
        => aModel.ProviderKind == ProviderKind.ClosedApi;

    /// <inheritdoc />
    public Task<Guid> SubmitAsync(GenerationJob aJob, CancellationToken aCancellationToken)
    {
        var vExecution = new ClosedApiExecution();
        objExecutions[aJob.JobId] = vExecution;

        if (!objOptions.IsConfigured)
        {
            // Graceful degrade: no crash — the job fails with an actionable remediation (FN-051).
            vExecution.Status = new JobStatus(
                JobState.Failed, 0, null,
                ClosedApiOptions.NotConfiguredMessage,
                "Add ClosedApi:ApiKey (fal.ai / Replicate / WaveSpeed) under Admin, or route this job to the Fast (local) or High (RunPod) tier.");
            objLogger.LogWarning("ClosedApi job {JobId} rejected: aggregator not configured", aJob.JobId);
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
        => Task.FromResult(0m); // The orchestrator prices closed-API jobs through the registry cost block.

    private async Task RunAsync(GenerationJob aJob, ClosedApiExecution aExecution)
    {
        try
        {
            // A real aggregator call (submit → poll status → download the produced clip) would run
            // here once a key is supplied. In this phase the configured path emits a deterministic
            // placeholder MP4 so the pipeline and asset plumbing are exercisable.
            var vStepDelay = TimeSpan.FromSeconds(1.5d / ProgressSteps);
            for (var vStep = 1; vStep < ProgressSteps; vStep++)
            {
                await Task.Delay(vStepDelay).ConfigureAwait(false);
                aExecution.Status = new JobStatus(JobState.Running, vStep * (100 / ProgressSteps), null, null, null);
            }

            var vResult = await PersistAssetAsync(aJob).ConfigureAwait(false);
            aExecution.Result = vResult;
            aExecution.Status = new JobStatus(JobState.Succeeded, 100, TimeSpan.Zero, null, null);
        }
        catch (Exception vException)
        {
            objLogger.LogError(vException, "ClosedApiProvider execution failed for job {JobId}", aJob.JobId);
            aExecution.Status = new JobStatus(
                JobState.Failed, aExecution.Status.Progress, null,
                $"Closed-API execution error: {vException.Message}",
                "Retry the job; if it persists, verify ClosedApi:ApiKey under Admin.");
        }
    }

    private async Task<AssetReference> PersistAssetAsync(GenerationJob aJob)
    {
        var vBytes = SyntheticVideo.BuildPlaceholderMp4();
        var vUserFolder = SanitizeForPath(aJob.UserId);
        var vStoragePath = await objAssetStorage
            .SaveAsync(Path.Combine(vUserFolder, "video", $"{aJob.JobId}.mp4"), vBytes)
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
                Generator = $"TrStudio ClosedApiProvider ({objOptions.Vendor}, placeholder)",
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

    /// <summary>Mutable in-memory execution record for one submitted closed-API job.</summary>
    private sealed class ClosedApiExecution
    {
        public JobStatus Status { get; set; } = new(JobState.Running, 0, null, null, null);
        public AssetReference? Result { get; set; }
    }
}
