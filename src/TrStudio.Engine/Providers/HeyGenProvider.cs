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
/// HeyGen avatar <see cref="IGenerationProvider"/> (FN-037/BRD-28), gated to
/// <see cref="ProviderKind.HeyGen"/> so it backs the Premium avatar tier while the resolver still
/// matches exactly one provider per model. GRACEFUL DEGRADE: with no <c>HeyGen:ApiKey</c>
/// (<c>Avatar:HeyGenApiKey</c>) configured — the EMPTY default — a submitted job resolves to a Failed
/// status carrying the actionable "HeyGen not configured" message plus a remediation hint
/// (REQ-FN-005 style) instead of crashing. The live HeyGen REST call is deferred until a real key is
/// supplied; with a key the provider emits a deterministic placeholder video asset in this phase.
/// </summary>
public class HeyGenProvider : IGenerationProvider
{
    private const int ProgressSteps = 6;

    private readonly AvatarOptions objOptions;
    private readonly IAssetStorage objAssetStorage;
    private readonly IAssetRepository objAssetRepository;
    private readonly ILogger<HeyGenProvider> objLogger;
    private readonly ConcurrentDictionary<Guid, HeyGenExecution> objExecutions = new();

    /// <summary>
    /// Initialises the provider.
    /// </summary>
    /// <param name="aOptions">Avatar options carrying the HeyGen API key (empty by default).</param>
    /// <param name="aAssetStorage">Local object store receiving the output binary.</param>
    /// <param name="aAssetRepository">Asset metadata persistence.</param>
    /// <param name="aLogger">Structured logger (never logs the API key).</param>
    public HeyGenProvider(
        AvatarOptions aOptions,
        IAssetStorage aAssetStorage,
        IAssetRepository aAssetRepository,
        ILogger<HeyGenProvider> aLogger)
    {
        objOptions = aOptions;
        objAssetStorage = aAssetStorage;
        objAssetRepository = aAssetRepository;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public bool Supports(GenerationTaskType aTask, RegistryModel aModel)
        => aModel.ProviderKind == ProviderKind.HeyGen;

    /// <inheritdoc />
    public Task<Guid> SubmitAsync(GenerationJob aJob, CancellationToken aCancellationToken)
    {
        var vExecution = new HeyGenExecution();
        objExecutions[aJob.JobId] = vExecution;

        if (!objOptions.HeyGenConfigured)
        {
            // Graceful degrade: no crash — the job fails with an actionable remediation (FN-037).
            vExecution.Status = new JobStatus(
                JobState.Failed, 0, null,
                AvatarOptions.HeyGenNotConfiguredMessage,
                "Add HeyGen:ApiKey (Avatar:HeyGenApiKey) under Admin, or run this avatar job at the Fast (local) or High (RunPod) tier.");
            objLogger.LogWarning("HeyGen job {JobId} rejected: provider not configured", aJob.JobId);
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
        => Task.FromResult(0m); // The orchestrator prices HeyGen jobs through the registry cost block.

    private async Task RunAsync(GenerationJob aJob, HeyGenExecution aExecution)
    {
        try
        {
            // A real HeyGen REST call (create video → poll status → download) would run here once a key
            // is supplied. In this phase the configured path emits a deterministic placeholder video.
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
            objLogger.LogError(vException, "HeyGenProvider execution failed for job {JobId}", aJob.JobId);
            aExecution.Status = new JobStatus(
                JobState.Failed, aExecution.Status.Progress, null,
                $"HeyGen execution error: {vException.Message}",
                "Retry the job; if it persists, verify HeyGen:ApiKey under Admin.");
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
                Generator = "TrStudio HeyGenProvider (placeholder)",
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

    /// <summary>Mutable in-memory execution record for one submitted HeyGen job.</summary>
    private sealed class HeyGenExecution
    {
        public JobStatus Status { get; set; } = new(JobState.Running, 0, null, null, null);
        public AssetReference? Result { get; set; }
    }
}
