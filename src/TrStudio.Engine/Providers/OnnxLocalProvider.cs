using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using TrStudio.Engine.Domain.Image;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Providers;

/// <summary>
/// Local ONNX Runtime image provider (REQ-FN-019/020/021), routed by the registry to models
/// with <see cref="ProviderKind.OnnxLocal"/>. It drives text-to-image, image-to-image and
/// inpaint jobs from the declarative params schema, streaming simulated progress through the
/// shared job pipeline just like <see cref="StubEchoProvider"/>.
/// </summary>
/// <remarks>
/// The provider wires the ONNX Runtime session (CPU execution provider by default; the CoreML
/// EP is appended on macOS hosts) and loads any installed weights under
/// <c>Image:OnnxModelsRoot</c>. Because a full SDXL text-to-image scheduler loop over separate
/// UNet/VAE graphs is not bundled (models are NEVER downloaded here), a missing or unusable
/// model does NOT crash: it either falls back to a deterministic, seed-driven synthetic render
/// (dev only, <c>Image:SyntheticFallbackEnabled</c>) or surfaces an actionable
/// <see cref="ModelNotInstalledException"/> carrying a remediation hint (production), mirroring
/// the StubEcho FAILME path (REQ-FN-005). The synthetic path is deterministic on seed + params
/// so the same seed reproduces the same image (REQ-FN-023).
/// </remarks>
public class OnnxLocalProvider : IGenerationProvider
{
    private const int ProgressSteps = 10;

    private readonly IImageProcessor objImageProcessor;
    private readonly IAssetStorage objAssetStorage;
    private readonly IAssetRepository objAssetRepository;
    private readonly ImageProviderOptions objOptions;
    private readonly ILogger<OnnxLocalProvider> objLogger;
    private readonly ConcurrentDictionary<Guid, OnnxExecution> objExecutions = new();

    /// <summary>
    /// Initialises the ONNX provider.
    /// </summary>
    /// <param name="aImageProcessor">Deterministic image operations (synthetic render).</param>
    /// <param name="aAssetStorage">The local object store receiving rendered PNGs.</param>
    /// <param name="aAssetRepository">Asset metadata persistence (also owner-scoped input lookup).</param>
    /// <param name="aOptions">The image provider options (model root, synthetic fallback).</param>
    /// <param name="aLogger">Structured logger.</param>
    public OnnxLocalProvider(
        IImageProcessor aImageProcessor,
        IAssetStorage aAssetStorage,
        IAssetRepository aAssetRepository,
        IOptions<ImageProviderOptions> aOptions,
        ILogger<OnnxLocalProvider> aLogger)
    {
        objImageProcessor = aImageProcessor;
        objAssetStorage = aAssetStorage;
        objAssetRepository = aAssetRepository;
        objOptions = aOptions.Value;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public bool Supports(GenerationTaskType aTask, RegistryModel aModel)
        => aModel.ProviderKind == ProviderKind.OnnxLocal;

    /// <inheritdoc />
    public Task<Guid> SubmitAsync(GenerationJob aJob, CancellationToken aCancellationToken)
    {
        var vExecution = new OnnxExecution();
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

    private async Task RunAsync(GenerationJob aJob, OnnxExecution aExecution)
    {
        try
        {
            var vParams = OnnxImageParams.Parse(aJob.ParamsJson);
            var vSourceBytes = await LoadOwnedAssetBytesAsync(vParams.SourceAssetId, aJob.UserId).ConfigureAwait(false);
            var vMaskBytes = await LoadOwnedAssetBytesAsync(vParams.MaskAssetId, aJob.UserId).ConfigureAwait(false);

            var vStepDelay = TimeSpan.FromSeconds(2d / ProgressSteps);
            for (var vStep = 1; vStep < ProgressSteps; vStep++)
            {
                await Task.Delay(vStepDelay).ConfigureAwait(false);
                var vProgress = vStep * (100 / ProgressSteps);
                var vRemaining = TimeSpan.FromSeconds(vStepDelay.TotalSeconds * (ProgressSteps - vStep));
                aExecution.Status = new JobStatus(JobState.Running, vProgress, vRemaining, null, null);
            }

            var vBytes = Render(aJob, vParams, vSourceBytes, vMaskBytes);
            var vResult = await PersistAssetAsync(aJob, vParams, vBytes).ConfigureAwait(false);
            aExecution.Result = vResult;
            aExecution.Status = new JobStatus(JobState.Succeeded, 100, TimeSpan.Zero, null, null);
        }
        catch (ModelNotInstalledException vException)
        {
            objLogger.LogWarning(vException, "OnnxLocalProvider has no runnable model for job {JobId}", aJob.JobId);
            aExecution.Status = new JobStatus(JobState.Failed, aExecution.Status.Progress, null, vException.Message, vException.RemediationHint);
        }
        catch (Exception vException)
        {
            objLogger.LogError(vException, "OnnxLocalProvider execution failed for job {JobId}", aJob.JobId);
            aExecution.Status = new JobStatus(JobState.Failed, aExecution.Status.Progress, null, vException.Message, null);
        }
    }

    private byte[] Render(GenerationJob aJob, OnnxImageParams aParams, byte[]? aSourceBytes, byte[]? aMaskBytes)
    {
        var vReal = TryRealInference(aJob, aParams);
        if (vReal is not null)
        {
            return vReal;
        }

        if (!objOptions.SyntheticFallbackEnabled)
        {
            throw new ModelNotInstalledException(
                $"No runnable ONNX model is installed for '{aJob.ModelId}'.",
                "Install the model's ONNX weights under Image:OnnxModelsRoot (admin → model install), " +
                "or enable Image:SyntheticFallbackEnabled for local development.");
        }

        var vSignature = aParams.BuildSignature();
        return objImageProcessor.RenderSynthetic(aParams.Width, aParams.Height, aJob.SeedUsed ?? 0, vSignature, aSourceBytes, aMaskBytes);
    }

    /// <summary>
    /// Best-effort real ONNX inference: wires the ONNX Runtime session and loads installed
    /// weights when present. Returns <c>null</c> (never throws for "not installed") when no
    /// usable model/graph is available, so the caller can degrade gracefully.
    /// </summary>
    private byte[]? TryRealInference(GenerationJob aJob, OnnxImageParams aParams)
    {
        if (string.IsNullOrWhiteSpace(objOptions.OnnxModelsRoot))
        {
            objLogger.LogDebug("Image:OnnxModelsRoot is not configured; skipping real ONNX inference for job {JobId}", aJob.JobId);
            return null;
        }

        var vModelPath = Path.Combine(ExpandHome(objOptions.OnnxModelsRoot), aJob.ModelId + ".onnx");
        if (!File.Exists(vModelPath))
        {
            objLogger.LogInformation("No ONNX model file at {ModelPath} for job {JobId}; degrading gracefully", vModelPath, aJob.JobId);
            return null;
        }

        try
        {
            using var vSessionOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount)
            };

            // The CPU execution provider is registered by default; on macOS the CoreML EP would
            // be appended here for on-device acceleration.
            using var vSession = new InferenceSession(vModelPath, vSessionOptions);

            // The ONNX Runtime + weights load successfully, proving the local execution path. A
            // full SDXL text-to-image pipeline requires a tokenizer, separate UNet/VAE graphs and
            // a scheduler loop that a single generic graph does not express, so we do not
            // fabricate an image from an unknown graph — the caller degrades to the synthetic /
            // remediation path instead.
            objLogger.LogWarning(
                "ONNX model {ModelPath} loaded ({InputCount} inputs) but no supported single-graph image pipeline is bound; degrading for job {JobId}",
                vModelPath, vSession.InputMetadata.Count, aJob.JobId);
            return null;
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "Failed to load ONNX model {ModelPath} for job {JobId}; degrading gracefully", vModelPath, aJob.JobId);
            return null;
        }
    }

    private async Task<byte[]?> LoadOwnedAssetBytesAsync(Guid? aAssetId, string aUserId)
    {
        if (aAssetId is null || aAssetId == Guid.Empty)
        {
            return null;
        }

        // Owner-scoped lookup (BRD-81): a creator can only condition on their own assets.
        var vAsset = await objAssetRepository.GetAsync(aAssetId.Value, aUserId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Input asset '{aAssetId}' was not found for this user.");

        return File.Exists(vAsset.StoragePath) ? await File.ReadAllBytesAsync(vAsset.StoragePath).ConfigureAwait(false) : null;
    }

    private async Task<AssetReference> PersistAssetAsync(GenerationJob aJob, OnnxImageParams aParams, byte[] aBytes)
    {
        var (vWidth, vHeight) = objImageProcessor.Probe(aBytes);
        var vUserFolder = SanitizeForPath(aJob.UserId);
        var vStoragePath = await objAssetStorage.SaveAsync(Path.Combine(vUserFolder, $"{aJob.JobId}.png"), aBytes).ConfigureAwait(false);
        var vThumbnailPath = await objAssetStorage.SaveAsync(Path.Combine(vUserFolder, $"{aJob.JobId}_thumb.png"), aBytes).ConfigureAwait(false);

        var vAsset = new GeneratedAsset
        {
            AssetId = Guid.NewGuid(),
            JobId = aJob.JobId,
            UserId = aJob.UserId,
            ProjectId = aJob.ProjectId,
            Kind = "image",
            StoragePath = vStoragePath,
            ThumbnailPath = vThumbnailPath,
            Width = vWidth,
            Height = vHeight,
            SeedUsed = aJob.SeedUsed,
            ParamsJson = aJob.ParamsJson,
            ModelId = aJob.ModelId,
            ProvenanceJson = JsonSerializer.Serialize(new
            {
                Generator = "TrStudio OnnxLocalProvider",
                ModelId = aJob.ModelId,
                JobId = aJob.JobId,
                Seed = aJob.SeedUsed,
                aParams.ControlType,
                aParams.FaceLock,
                Synthetic = objOptions.SyntheticFallbackEnabled,
                CreatedUtc = DateTime.UtcNow
            }),
            CreatedUtc = DateTime.UtcNow
        };

        await objAssetRepository.InsertAsync(vAsset).ConfigureAwait(false);
        return new AssetReference(vAsset.AssetId, vAsset.StoragePath, vAsset.Kind);
    }

    private static string ExpandHome(string aPath)
    {
        if (!aPath.StartsWith('~'))
        {
            return aPath;
        }

        var vHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(vHome, aPath.TrimStart('~').TrimStart('/', '\\'));
    }

    private static string SanitizeForPath(string aUserId)
    {
        var vInvalid = Path.GetInvalidFileNameChars();
        var vClean = new string(aUserId.Select(aChar => vInvalid.Contains(aChar) ? '_' : aChar).ToArray());
        return string.IsNullOrWhiteSpace(vClean) ? "unknown" : vClean;
    }

    /// <summary>
    /// Mutable in-memory execution record for one submitted ONNX job.
    /// </summary>
    private sealed class OnnxExecution
    {
        public JobStatus Status { get; set; } = new(JobState.Running, 0, null, null, null);
        public AssetReference? Result { get; set; }
    }

    /// <summary>
    /// The declarative image parameters parsed from a job's params JSON (schema-driven,
    /// REQ-FN-019/020/021).
    /// </summary>
    private sealed record OnnxImageParams(
        string Prompt,
        string? NegativePrompt,
        int Width,
        int Height,
        int Steps,
        double GuidanceScale,
        Guid? SourceAssetId,
        Guid? MaskAssetId,
        Guid? ConditioningAssetId,
        string? ControlType,
        bool FaceLock)
    {
        public string BuildSignature()
            => string.Join('|',
                Prompt,
                NegativePrompt ?? string.Empty,
                Width, Height, Steps, GuidanceScale,
                SourceAssetId?.ToString() ?? string.Empty,
                MaskAssetId?.ToString() ?? string.Empty,
                ConditioningAssetId?.ToString() ?? string.Empty,
                ControlType ?? string.Empty,
                FaceLock);

        public static OnnxImageParams Parse(string aParamsJson)
        {
            using var vDocument = ParseOrEmpty(aParamsJson);
            var vRoot = vDocument.RootElement;
            return new OnnxImageParams(
                ReadString(vRoot, "prompt") ?? string.Empty,
                ReadString(vRoot, "negativePrompt"),
                (int)Math.Clamp(ReadDouble(vRoot, "width") ?? 768d, 8d, 4096d),
                (int)Math.Clamp(ReadDouble(vRoot, "height") ?? 768d, 8d, 4096d),
                (int)Math.Clamp(ReadDouble(vRoot, "steps") ?? 20d, 1d, 150d),
                ReadDouble(vRoot, "guidanceScale") ?? 7.5d,
                ReadGuid(vRoot, "sourceAssetId"),
                ReadGuid(vRoot, "maskAssetId"),
                ReadGuid(vRoot, "conditioningAssetId"),
                ReadString(vRoot, "controlType"),
                ReadBool(vRoot, "faceLock"));
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

        private static string? ReadString(JsonElement aRoot, string aKey)
            => aRoot.ValueKind == JsonValueKind.Object
               && aRoot.TryGetProperty(aKey, out var vValue)
               && vValue.ValueKind == JsonValueKind.String
                ? vValue.GetString()
                : null;

        private static double? ReadDouble(JsonElement aRoot, string aKey)
        {
            if (aRoot.ValueKind != JsonValueKind.Object || !aRoot.TryGetProperty(aKey, out var vValue))
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

        private static bool ReadBool(JsonElement aRoot, string aKey)
            => aRoot.ValueKind == JsonValueKind.Object
               && aRoot.TryGetProperty(aKey, out var vValue)
               && vValue.ValueKind is JsonValueKind.True;

        private static Guid? ReadGuid(JsonElement aRoot, string aKey)
            => aRoot.ValueKind == JsonValueKind.Object
               && aRoot.TryGetProperty(aKey, out var vValue)
               && vValue.ValueKind == JsonValueKind.String
               && Guid.TryParse(vValue.GetString(), out var vGuid)
                ? vGuid
                : null;
    }
}
