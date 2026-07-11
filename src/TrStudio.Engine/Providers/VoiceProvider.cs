using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Voice;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Providers;

/// <summary>
/// Voice <see cref="IGenerationProvider"/> (FN-031/033/BRD-22..24) registered alongside the other
/// providers and gated by <see cref="ProviderKind.Voice"/> so the orchestrator still resolves exactly
/// one provider per model. It backs registry-driven TextToSpeech jobs on the spine. GRACEFUL DEGRADE:
/// with no real voice model installed the job fails with an actionable "voice model not installed"
/// message, unless the dev synthetic fallback is on, in which case it emits a deterministic silent
/// WAV asset (mirroring the StubEcho pattern). Real per-language synthesis is wired in a later phase.
/// </summary>
public class VoiceProvider : IGenerationProvider
{
    private readonly VoiceOptions objOptions;
    private readonly IAssetStorage objAssetStorage;
    private readonly IAssetRepository objAssetRepository;
    private readonly ILogger<VoiceProvider> objLogger;
    private readonly ConcurrentDictionary<Guid, VoiceExecution> objExecutions = new();

    /// <summary>
    /// Initialises the provider.
    /// </summary>
    /// <param name="aOptions">Voice options (installed models, synthetic fallback).</param>
    /// <param name="aAssetStorage">Local object store receiving the audio binary.</param>
    /// <param name="aAssetRepository">Asset metadata persistence.</param>
    /// <param name="aLogger">Structured logger.</param>
    public VoiceProvider(
        VoiceOptions aOptions,
        IAssetStorage aAssetStorage,
        IAssetRepository aAssetRepository,
        ILogger<VoiceProvider> aLogger)
    {
        objOptions = aOptions;
        objAssetStorage = aAssetStorage;
        objAssetRepository = aAssetRepository;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public bool Supports(GenerationTaskType aTask, RegistryModel aModel)
        => aModel.ProviderKind == ProviderKind.Voice;

    /// <inheritdoc />
    public Task<Guid> SubmitAsync(GenerationJob aJob, CancellationToken aCancellationToken)
    {
        var vExecution = new VoiceExecution();
        objExecutions[aJob.JobId] = vExecution;

        if (!objOptions.SyntheticFallback && !objOptions.SynthesisEnabled)
        {
            vExecution.Status = new JobStatus(
                JobState.Failed, 0, null,
                VoiceOptions.ModelNotInstalledMessage,
                "Install a TTS voice model under Admin > Models, or enable Voice:SyntheticFallback in dev.");
            objLogger.LogWarning("Voice job {JobId} rejected: no voice model installed", aJob.JobId);
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
        => Task.FromResult(0m); // Local synthetic voice is free; billed voice backends are priced via the registry.

    private async Task RunAsync(GenerationJob aJob, VoiceExecution aExecution)
    {
        try
        {
            var vText = ReadStringParam(aJob.ParamsJson, "text") ?? string.Empty;
            var vSpeed = ReadDoubleParam(aJob.ParamsJson, "speed") ?? 1.0d;
            var vDuration = SyntheticAudio.EstimateDurationSeconds(vText, vSpeed);

            aExecution.Status = new JobStatus(JobState.Running, 40, TimeSpan.FromSeconds(vDuration), null, null);

            var vBytes = SyntheticAudio.BuildSilentWav(vDuration);
            var vResult = await PersistAssetAsync(aJob, vBytes).ConfigureAwait(false);

            aExecution.Result = vResult;
            aExecution.Status = new JobStatus(JobState.Succeeded, 100, TimeSpan.Zero, null, null);
        }
        catch (Exception vException)
        {
            objLogger.LogError(vException, "VoiceProvider execution failed for job {JobId}", aJob.JobId);
            aExecution.Status = new JobStatus(JobState.Failed, aExecution.Status.Progress, null, vException.Message, null);
        }
    }

    private async Task<AssetReference> PersistAssetAsync(GenerationJob aJob, byte[] aBytes)
    {
        var vUserFolder = SanitizeForPath(aJob.UserId);
        var vStoragePath = await objAssetStorage
            .SaveAsync(Path.Combine(vUserFolder, "voice", $"{aJob.JobId}.wav"), aBytes)
            .ConfigureAwait(false);

        var vAsset = new GeneratedAsset
        {
            AssetId = Guid.NewGuid(),
            JobId = aJob.JobId,
            UserId = aJob.UserId,
            ProjectId = aJob.ProjectId,
            Kind = "audio",
            StoragePath = vStoragePath,
            ThumbnailPath = null,
            Width = null,
            Height = null,
            SeedUsed = aJob.SeedUsed,
            ParamsJson = aJob.ParamsJson,
            ModelId = aJob.ModelId,
            ProvenanceJson = JsonSerializer.Serialize(new
            {
                Generator = "TrStudio VoiceProvider (synthetic-dev)",
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

    /// <summary>Mutable in-memory execution record for one submitted voice job.</summary>
    private sealed class VoiceExecution
    {
        public JobStatus Status { get; set; } = new(JobState.Running, 0, null, null, null);
        public AssetReference? Result { get; set; }
    }
}
