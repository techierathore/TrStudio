using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.RunPod;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Providers;

/// <summary>
/// RunPod serverless <see cref="IGenerationProvider"/> (REQ-FN-028/BRD-12). Submits to a RunPod
/// serverless endpoint, polls run status, and — when auto idle-stop is on — scales the endpoint
/// to zero after the job finishes (scale-to-zero). GRACEFUL DEGRADE: with no <c>RunPod:ApiKey</c>
/// configured, a submitted job resolves to a Failed status carrying the actionable
/// "RunPod not configured" message plus a remediation hint (REQ-FN-005 style) instead of
/// crashing; live cold-start is deferred until a real key is supplied.
/// </summary>
public class RunPodProvider : IGenerationProvider
{
    /// <summary>A minimal valid 1x1 PNG used as the placeholder output binary until real output decoding is wired.</summary>
    private const string PlaceholderPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly IRunPodClient objClient;
    private readonly RunPodOptions objOptions;
    private readonly IAssetStorage objAssetStorage;
    private readonly IAssetRepository objAssetRepository;
    private readonly ILogger<RunPodProvider> objLogger;
    private readonly ConcurrentDictionary<Guid, RunPodExecution> objExecutions = new();

    /// <summary>
    /// Initialises the provider.
    /// </summary>
    /// <param name="aClient">The RunPod REST client seam.</param>
    /// <param name="aOptions">Shared RunPod options (config + idle-stop toggle).</param>
    /// <param name="aAssetStorage">The local object store receiving the output binary.</param>
    /// <param name="aAssetRepository">Asset metadata persistence.</param>
    /// <param name="aLogger">Structured logger (never logs the API key).</param>
    public RunPodProvider(
        IRunPodClient aClient,
        RunPodOptions aOptions,
        IAssetStorage aAssetStorage,
        IAssetRepository aAssetRepository,
        ILogger<RunPodProvider> aLogger)
    {
        objClient = aClient;
        objOptions = aOptions;
        objAssetStorage = aAssetStorage;
        objAssetRepository = aAssetRepository;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public bool Supports(GenerationTaskType aTask, RegistryModel aModel)
        => aModel.ProviderKind == ProviderKind.RunPod;

    /// <inheritdoc />
    public Task<Guid> SubmitAsync(GenerationJob aJob, CancellationToken aCancellationToken)
    {
        var vExecution = new RunPodExecution();
        objExecutions[aJob.JobId] = vExecution;

        if (!objOptions.IsConfigured)
        {
            // Graceful degrade: no crash — the job fails with an actionable remediation.
            vExecution.Status = new JobStatus(
                JobState.Failed,
                0,
                null,
                RunPodOptions.NotConfiguredMessage,
                "Add RunPod:ApiKey and RunPod:EndpointId under Admin > RunPod, or run this job on the free local (Fast) tier.");
            objLogger.LogWarning("RunPod job {JobId} rejected: provider not configured", aJob.JobId);
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
        => Task.FromResult(0m); // The orchestrator prices RunPod jobs through ICostEstimator (registry cost block).

    private async Task RunAsync(GenerationJob aJob, RunPodExecution aExecution)
    {
        try
        {
            var vRunId = await objClient.SubmitAsync(aJob.ParamsJson, CancellationToken.None).ConfigureAwait(false);
            aExecution.Status = new JobStatus(JobState.Running, 5, null, null, null);

            while (true)
            {
                await Task.Delay(PollInterval).ConfigureAwait(false);
                var vStatus = await objClient.GetRunStatusAsync(vRunId, CancellationToken.None).ConfigureAwait(false);
                var vToken = (vStatus.Status ?? string.Empty).ToUpperInvariant();

                if (vToken is "COMPLETED")
                {
                    var vResult = await PersistAssetAsync(aJob).ConfigureAwait(false);
                    aExecution.Result = vResult;
                    aExecution.Status = new JobStatus(JobState.Succeeded, 100, TimeSpan.Zero, null, null);
                    await IdleStopIfEnabledAsync().ConfigureAwait(false);
                    return;
                }

                if (vToken is "FAILED" or "CANCELLED" or "TIMED_OUT")
                {
                    var vFailure = vStatus.Error ?? $"RunPod run ended in state '{vStatus.Status}'.";
                    aExecution.Status = new JobStatus(
                        JobState.Failed, aExecution.Status.Progress, null, vFailure,
                        "Retry the job; if it persists, verify the RunPod endpoint and API key under Admin > RunPod.");
                    await IdleStopIfEnabledAsync().ConfigureAwait(false);
                    return;
                }

                aExecution.Status = new JobStatus(JobState.Running, Math.Clamp(vStatus.Progress, 1, 99), null, null, null);
            }
        }
        catch (Exception vException)
        {
            objLogger.LogError(vException, "RunPod execution failed for job {JobId}", aJob.JobId);
            aExecution.Status = new JobStatus(
                JobState.Failed, aExecution.Status.Progress, null,
                $"RunPod execution error: {vException.Message}",
                "Retry the job; if it persists, verify the RunPod endpoint and API key under Admin > RunPod.");
        }
    }

    private async Task IdleStopIfEnabledAsync()
    {
        if (!objOptions.AutoIdleStop)
        {
            return;
        }

        try
        {
            // Scale-to-zero after an idle job so the endpoint stops incurring cost (REQ-FN-028).
            await objClient.SetMinWorkersAsync(0, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception vException) when (vException is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            objLogger.LogWarning(vException, "RunPod auto idle-stop request failed");
        }
    }

    private async Task<AssetReference> PersistAssetAsync(GenerationJob aJob)
    {
        var vBytes = Convert.FromBase64String(PlaceholderPngBase64);
        var vUserFolder = SanitizeForPath(aJob.UserId);
        var vStoragePath = await objAssetStorage.SaveAsync(Path.Combine(vUserFolder, $"{aJob.JobId}.png"), vBytes).ConfigureAwait(false);
        var vThumbnailPath = await objAssetStorage.SaveAsync(Path.Combine(vUserFolder, $"{aJob.JobId}_thumb.png"), vBytes).ConfigureAwait(false);

        var vAsset = new GeneratedAsset
        {
            AssetId = Guid.NewGuid(),
            JobId = aJob.JobId,
            UserId = aJob.UserId,
            ProjectId = aJob.ProjectId,
            Kind = "image",
            StoragePath = vStoragePath,
            ThumbnailPath = vThumbnailPath,
            Width = 1,
            Height = 1,
            SeedUsed = aJob.SeedUsed,
            ParamsJson = aJob.ParamsJson,
            ModelId = aJob.ModelId,
            ProvenanceJson = JsonSerializer.Serialize(new
            {
                Generator = "TrStudio RunPodProvider",
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

    /// <summary>Mutable in-memory execution record for one submitted RunPod job.</summary>
    private sealed class RunPodExecution
    {
        public JobStatus Status { get; set; } = new(JobState.Running, 0, null, null, null);
        public AssetReference? Result { get; set; }
    }
}
