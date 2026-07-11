using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Providers;

/// <summary>
/// In-process stub <see cref="IGenerationProvider"/> proving the spine end-to-end
/// (REQ-FN-001 acceptance). It "generates" by writing a small placeholder PNG into the local
/// asset store with simulated progress over a few seconds, driven by the seeded
/// <c>stub-echo</c> registry row (dev-seed). A prompt containing <c>FAILME</c> fails
/// deterministically with a simulated out-of-memory message so the Hangfire retry policy and
/// actionable-failure path can be verified (REQ-FN-003/005).
/// </summary>
public class StubEchoProvider : IGenerationProvider
{
    /// <summary>A minimal valid 1x1 PNG used as the placeholder output binary.</summary>
    private const string PlaceholderPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private const int ProgressSteps = 10;

    private readonly IAssetStorage objAssetStorage;
    private readonly IAssetRepository objAssetRepository;
    private readonly ILogger<StubEchoProvider> objLogger;
    private readonly ConcurrentDictionary<Guid, StubExecution> objExecutions = new();

    /// <summary>
    /// Initialises the stub provider.
    /// </summary>
    /// <param name="aAssetStorage">The local object store receiving the placeholder PNG.</param>
    /// <param name="aAssetRepository">Asset metadata persistence.</param>
    /// <param name="aLogger">Structured logger.</param>
    public StubEchoProvider(IAssetStorage aAssetStorage, IAssetRepository aAssetRepository, ILogger<StubEchoProvider> aLogger)
    {
        objAssetStorage = aAssetStorage;
        objAssetRepository = aAssetRepository;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public bool Supports(GenerationTaskType aTask, RegistryModel aModel)
        => aModel.ProviderKind == ProviderKind.StubEcho;

    /// <inheritdoc />
    public Task<Guid> SubmitAsync(GenerationJob aJob, CancellationToken aCancellationToken)
    {
        var vExecution = new StubExecution();
        objExecutions[aJob.JobId] = vExecution;

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
        => Task.FromResult(0m);

    private async Task RunAsync(GenerationJob aJob, StubExecution aExecution)
    {
        try
        {
            var vPrompt = ReadStringParam(aJob.ParamsJson, "prompt") ?? string.Empty;
            var vDurationSeconds = Math.Clamp(ReadDoubleParam(aJob.ParamsJson, "durationSeconds") ?? 3d, 0.05d, 30d);
            var vStepDelay = TimeSpan.FromSeconds(vDurationSeconds / ProgressSteps);
            var vShouldFail = vPrompt.Contains("FAILME", StringComparison.Ordinal);

            for (var vStep = 1; vStep <= ProgressSteps; vStep++)
            {
                await Task.Delay(vStepDelay).ConfigureAwait(false);
                var vProgress = vStep * (100 / ProgressSteps);

                if (vShouldFail && vProgress >= 40)
                {
                    var vFailure = "Simulated provider failure: out of memory while rendering (FAILME).";
                    aExecution.Status = new JobStatus(JobState.Failed, vProgress, null, vFailure,
                        "Drop the resolution to 256px or route the job to the High tier.");
                    return;
                }

                var vRemaining = TimeSpan.FromSeconds(vStepDelay.TotalSeconds * (ProgressSteps - vStep));
                aExecution.Status = new JobStatus(JobState.Running, Math.Min(vProgress, 100), vRemaining, null, null);
            }

            var vResult = await PersistAssetAsync(aJob).ConfigureAwait(false);
            aExecution.Result = vResult;
            aExecution.Status = new JobStatus(JobState.Succeeded, 100, TimeSpan.Zero, null, null);
        }
        catch (Exception vException)
        {
            objLogger.LogError(vException, "StubEchoProvider execution failed for job {JobId}", aJob.JobId);
            aExecution.Status = new JobStatus(JobState.Failed, aExecution.Status.Progress, null, vException.Message, null);
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
                Generator = "TrStudio StubEchoProvider",
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

    private static string? ReadStringParam(string aParamsJson, string aKey)
    {
        using var vDocument = ParseOrEmpty(aParamsJson);
        return vDocument.RootElement.ValueKind == JsonValueKind.Object
               && vDocument.RootElement.TryGetProperty(aKey, out var vValue)
               && vValue.ValueKind == JsonValueKind.String
            ? vValue.GetString()
            : null;
    }

    private static double? ReadDoubleParam(string aParamsJson, string aKey)
    {
        using var vDocument = ParseOrEmpty(aParamsJson);
        if (vDocument.RootElement.ValueKind != JsonValueKind.Object
            || !vDocument.RootElement.TryGetProperty(aKey, out var vValue))
        {
            return null;
        }

        return vValue.ValueKind switch
        {
            JsonValueKind.Number => vValue.GetDouble(),
            JsonValueKind.String when double.TryParse(vValue.GetString(), out var vParsed) => vParsed,
            _ => null
        };
    }

    private static JsonDocument ParseOrEmpty(string aParamsJson)
    {
        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(aParamsJson) ? "{}" : aParamsJson);
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}");
        }
    }

    /// <summary>
    /// Mutable in-memory execution record for one submitted stub job.
    /// </summary>
    private sealed class StubExecution
    {
        public JobStatus Status { get; set; } = new(JobState.Running, 0, null, null, null);
        public AssetReference? Result { get; set; }
    }
}
