using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using TrStudio.Engine.Domain.Install;
using TrStudio.Engine.Domain.ModelSources;
using TrStudio.Engine.Workers.Install;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Infrastructure.Install;

/// <summary>
/// The model install service (REQ-FN-011): a persistent install queue over the "ModelInstall"
/// table with start/pause/resume/cancel, RESUMABLE downloads (HTTP Range into a .partial file,
/// renamed on completion), size/licence surfacing before install, live speed tracking, and the
/// destination default rule (&gt;10 GB → RunPod network volume, else the local models dir).
/// </summary>
public class ModelInstallService : IModelInstallService, IInstallPreviewService
{
    private readonly IModelInstallRepository objInstallRepository;
    private readonly IModelSourceRepository objSourceRepository;
    private readonly IModelSourceTokenStore objTokenStore;
    private readonly IInstallDownloader objDownloader;
    private readonly InstallDestinationPlanner objDestinationPlanner;
    private readonly IHttpClientFactory objHttpClientFactory;
    private readonly ILogger<ModelInstallService> objLogger;
    private readonly ConcurrentDictionary<Guid, InstallRuntime> objActiveInstalls = new();

    /// <summary>
    /// Initialises the service.
    /// </summary>
    /// <param name="aInstallRepository">Persistent install-row repository.</param>
    /// <param name="aSourceRepository">Model source repository.</param>
    /// <param name="aTokenStore">Encrypted source-token store (tokens are used for auth headers only).</param>
    /// <param name="aDownloader">Resumable downloader.</param>
    /// <param name="aDestinationPlanner">Destination default-rule planner.</param>
    /// <param name="aHttpClientFactory">Factory for the licence-probe HTTP client.</param>
    /// <param name="aLogger">Logger for install diagnostics.</param>
    public ModelInstallService(
        IModelInstallRepository aInstallRepository,
        IModelSourceRepository aSourceRepository,
        IModelSourceTokenStore aTokenStore,
        IInstallDownloader aDownloader,
        InstallDestinationPlanner aDestinationPlanner,
        IHttpClientFactory aHttpClientFactory,
        ILogger<ModelInstallService> aLogger)
    {
        objInstallRepository = aInstallRepository;
        objSourceRepository = aSourceRepository;
        objTokenStore = aTokenStore;
        objDownloader = aDownloader;
        objDestinationPlanner = aDestinationPlanner;
        objHttpClientFactory = aHttpClientFactory;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<ModelInstallItem> StartInstallAsync(string aModelName, Guid aSourceId, string aDestination, CancellationToken aCancellationToken = default)
    {
        var vSource = await RequireSourceAsync(aSourceId, aCancellationToken).ConfigureAwait(false);
        if (!vSource.IsEnabled)
        {
            throw new InvalidOperationException($"Model source '{vSource.Name}' is disabled — enable it before installing models from it.");
        }

        var vUrl = ResolveDownloadUrl(vSource.BaseUrl, aModelName);
        var vToken = await objTokenStore.GetTokenAsync(aSourceId, aCancellationToken).ConfigureAwait(false);
        var vProbe = await TryProbeAsync(vUrl, vToken, aCancellationToken).ConfigureAwait(false);

        var vItem = new ModelInstallItem
        {
            InstallId = Guid.NewGuid(),
            ModelName = aModelName,
            SourceId = aSourceId,
            DownloadUrl = vUrl,
            TotalBytes = vProbe?.ContentLength,
            DownloadedBytes = 0,
            State = InstallStates.Queued,
            Destination = objDestinationPlanner.PlanDestination(vProbe?.ContentLength, aDestination),
            CreatedUtc = DateTime.UtcNow
        };
        await objInstallRepository.InsertAsync(vItem, aCancellationToken).ConfigureAwait(false);
        LaunchDownload(vItem.InstallId);
        return vItem;
    }

    /// <inheritdoc />
    public async Task PauseAsync(Guid aInstallId, CancellationToken aCancellationToken = default)
    {
        var vItem = await RequireInstallAsync(aInstallId, aCancellationToken).ConfigureAwait(false);
        if (objActiveInstalls.TryGetValue(aInstallId, out var vRuntime))
        {
            vRuntime.IsPauseRequested = true;
            vRuntime.Cts.Cancel();
            return;
        }

        if (vItem.State is InstallStates.Downloading or InstallStates.Queued)
        {
            await PersistStateAsync(vItem, InstallStates.Paused, null, aCancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task ResumeAsync(Guid aInstallId, CancellationToken aCancellationToken = default)
    {
        var vItem = await RequireInstallAsync(aInstallId, aCancellationToken).ConfigureAwait(false);
        if (objActiveInstalls.ContainsKey(aInstallId))
        {
            return;
        }

        if (vItem.State is not (InstallStates.Paused or InstallStates.Failed or InstallStates.Queued or InstallStates.Downloading))
        {
            throw new InvalidOperationException($"Install '{vItem.ModelName}' is {vItem.State} and cannot be resumed.");
        }

        LaunchDownload(aInstallId);
    }

    /// <inheritdoc />
    public async Task CancelAsync(Guid aInstallId, CancellationToken aCancellationToken = default)
    {
        var vItem = await RequireInstallAsync(aInstallId, aCancellationToken).ConfigureAwait(false);
        if (objActiveInstalls.TryGetValue(aInstallId, out var vRuntime))
        {
            vRuntime.IsPauseRequested = false;
            vRuntime.Cts.Cancel();
            return;
        }

        DeletePartialFile(vItem);
        await PersistStateAsync(vItem, InstallStates.Cancelled, null, aCancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModelInstallItem>> ListAsync(CancellationToken aCancellationToken = default)
    {
        var vItems = await objInstallRepository.ListAsync(aCancellationToken).ConfigureAwait(false);
        foreach (var vItem in vItems)
        {
            await NormalizeOrphanAsync(vItem, aCancellationToken).ConfigureAwait(false);
        }

        return vItems;
    }

    /// <inheritdoc />
    public async Task<ModelInstallItem> PreviewAsync(string aModelName, Guid aSourceId, CancellationToken aCancellationToken = default)
    {
        var vSource = await RequireSourceAsync(aSourceId, aCancellationToken).ConfigureAwait(false);
        var vUrl = ResolveDownloadUrl(vSource.BaseUrl, aModelName);
        var vToken = await objTokenStore.GetTokenAsync(aSourceId, aCancellationToken).ConfigureAwait(false);
        var vProbe = await TryProbeAsync(vUrl, vToken, aCancellationToken).ConfigureAwait(false);
        var vLicense = vSource.Kind == ModelSourceKind.HuggingFace
            ? await TryProbeHuggingFaceLicenseAsync(vSource.BaseUrl, aModelName, vToken, aCancellationToken).ConfigureAwait(false)
            : null;

        return new ModelInstallItem
        {
            InstallId = Guid.Empty,
            ModelName = aModelName,
            SourceId = aSourceId,
            DownloadUrl = vUrl,
            TotalBytes = vProbe?.ContentLength,
            State = "preview",
            Destination = objDestinationPlanner.PlanDestination(vProbe?.ContentLength, null),
            LicenseName = vLicense,
            CreatedUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Resolves the download URL for a model reference: an absolute http(s) URL wins, anything
    /// else is appended to the source base URL.
    /// </summary>
    /// <param name="aBaseUrl">The source base URL.</param>
    /// <param name="aModelName">The model reference (absolute URL or relative path).</param>
    /// <returns>The absolute download URL.</returns>
    public static string ResolveDownloadUrl(string aBaseUrl, string aModelName)
    {
        if (Uri.TryCreate(aModelName, UriKind.Absolute, out var vAbsolute)
            && (vAbsolute.Scheme == Uri.UriSchemeHttp || vAbsolute.Scheme == Uri.UriSchemeHttps))
        {
            return aModelName;
        }

        return $"{aBaseUrl.TrimEnd('/')}/{aModelName.TrimStart('/')}";
    }

    private void LaunchDownload(Guid aInstallId)
    {
        var vRuntime = new InstallRuntime();
        if (!objActiveInstalls.TryAdd(aInstallId, vRuntime))
        {
            return;
        }

        _ = Task.Run(() => RunDownloadAsync(aInstallId, vRuntime), CancellationToken.None);
    }

    private async Task RunDownloadAsync(Guid aInstallId, InstallRuntime aRuntime)
    {
        ModelInstallItem? vItem = null;
        try
        {
            vItem = await objInstallRepository.GetAsync(aInstallId, CancellationToken.None).ConfigureAwait(false);
            if (vItem is null)
            {
                return;
            }

            await PersistStateAsync(vItem, InstallStates.Downloading, null, CancellationToken.None).ConfigureAwait(false);
            var vToken = await objTokenStore.GetTokenAsync(vItem.SourceId, CancellationToken.None).ConfigureAwait(false);
            Directory.CreateDirectory(vItem.Destination);

            var vTargetPath = ResolveTargetFilePath(vItem);
            var vProgressSink = new Progress<InstallDownloadProgress>(aSample => PersistProgress(vItem, aSample));
            var vResult = await objDownloader.DownloadAsync(
                new InstallDownloadRequest { Url = vItem.DownloadUrl, TargetFilePath = vTargetPath, BearerToken = vToken },
                vProgressSink,
                aRuntime.Cts.Token).ConfigureAwait(false);

            vItem.TotalBytes = vResult.TotalBytes;
            vItem.DownloadedBytes = vResult.TotalBytes;
            vItem.SpeedBytesPerSec = null;
            await PersistStateAsync(vItem, InstallStates.Completed, null, CancellationToken.None).ConfigureAwait(false);
            objLogger.LogInformation("Model install {InstallId} completed: {Target}", aInstallId, vTargetPath);
        }
        catch (OperationCanceledException)
        {
            await HandleCancellationAsync(vItem, aRuntime).ConfigureAwait(false);
        }
        catch (HttpRequestException vHttpException)
        {
            await HandleFailureAsync(vItem, MapHttpError(vHttpException)).ConfigureAwait(false);
        }
        catch (Exception vException)
        {
            objLogger.LogError(vException, "Model install {InstallId} failed", aInstallId);
            await HandleFailureAsync(vItem, $"The download failed unexpectedly: {vException.Message}. Resume the install to retry — completed bytes are kept.").ConfigureAwait(false);
        }
        finally
        {
            objActiveInstalls.TryRemove(aInstallId, out _);
            aRuntime.Cts.Dispose();
        }
    }

    private async Task HandleCancellationAsync(ModelInstallItem? aItem, InstallRuntime aRuntime)
    {
        if (aItem is null)
        {
            return;
        }

        aItem.DownloadedBytes = GetPartialLength(aItem);
        aItem.SpeedBytesPerSec = null;
        if (aRuntime.IsPauseRequested)
        {
            await PersistStateAsync(aItem, InstallStates.Paused, null, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        DeletePartialFile(aItem);
        aItem.DownloadedBytes = 0;
        await PersistStateAsync(aItem, InstallStates.Cancelled, null, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task HandleFailureAsync(ModelInstallItem? aItem, string aActionableMessage)
    {
        if (aItem is null)
        {
            return;
        }

        aItem.DownloadedBytes = GetPartialLength(aItem);
        aItem.SpeedBytesPerSec = null;
        await PersistStateAsync(aItem, InstallStates.Failed, aActionableMessage, CancellationToken.None).ConfigureAwait(false);
    }

    private void PersistProgress(ModelInstallItem aItem, InstallDownloadProgress aSample)
    {
        aItem.DownloadedBytes = aSample.DownloadedBytes;
        aItem.TotalBytes = aSample.TotalBytes ?? aItem.TotalBytes;
        aItem.SpeedBytesPerSec = aSample.SpeedBytesPerSec;
        try
        {
            objInstallRepository.UpdateAsync(aItem, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "Persisting install progress for {InstallId} failed", aItem.InstallId);
        }
    }

    private async Task PersistStateAsync(ModelInstallItem aItem, string aState, string? aErrorMessage, CancellationToken aCancellationToken)
    {
        aItem.State = aState;
        aItem.ErrorMessage = aErrorMessage;
        await objInstallRepository.UpdateAsync(aItem, aCancellationToken).ConfigureAwait(false);
    }

    private async Task NormalizeOrphanAsync(ModelInstallItem aItem, CancellationToken aCancellationToken)
    {
        var vIsOrphan = aItem.State == InstallStates.Downloading && !objActiveInstalls.ContainsKey(aItem.InstallId);
        if (vIsOrphan)
        {
            aItem.DownloadedBytes = GetPartialLength(aItem);
            aItem.SpeedBytesPerSec = null;
            await PersistStateAsync(aItem, InstallStates.Paused, null, aCancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ModelSource> RequireSourceAsync(Guid aSourceId, CancellationToken aCancellationToken)
    {
        var vSource = await objSourceRepository.GetAsync(aSourceId, aCancellationToken).ConfigureAwait(false);
        return vSource ?? throw new InvalidOperationException($"Model source '{aSourceId}' was not found — add it under admin → sources first.");
    }

    private async Task<ModelInstallItem> RequireInstallAsync(Guid aInstallId, CancellationToken aCancellationToken)
    {
        var vItem = await objInstallRepository.GetAsync(aInstallId, aCancellationToken).ConfigureAwait(false);
        return vItem ?? throw new InvalidOperationException($"Install '{aInstallId}' was not found.");
    }

    private async Task<InstallProbeResult?> TryProbeAsync(string aUrl, string? aToken, CancellationToken aCancellationToken)
    {
        try
        {
            return await objDownloader.ProbeAsync(aUrl, aToken, aCancellationToken).ConfigureAwait(false);
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "Size probe failed for {Url}", aUrl);
            return null;
        }
    }

    private async Task<string?> TryProbeHuggingFaceLicenseAsync(string aBaseUrl, string aModelName, string? aToken, CancellationToken aCancellationToken)
    {
        try
        {
            var vSegments = aModelName.TrimStart('/').Split('/');
            if (vSegments.Length < 2)
            {
                return null;
            }

            var vApiUrl = $"{aBaseUrl.TrimEnd('/')}/api/models/{vSegments[0]}/{vSegments[1]}";
            var vClient = objHttpClientFactory.CreateClient(InstallDownloader.HttpClientName);
            using var vRequest = new HttpRequestMessage(HttpMethod.Get, vApiUrl);
            if (!string.IsNullOrWhiteSpace(aToken))
            {
                vRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", aToken);
            }

            using var vResponse = await vClient.SendAsync(vRequest, aCancellationToken).ConfigureAwait(false);
            if (!vResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var vJson = JObject.Parse(await vResponse.Content.ReadAsStringAsync(aCancellationToken).ConfigureAwait(false));
            return vJson["cardData"]?["license"]?.ToString() ?? vJson["license"]?.ToString();
        }
        catch (Exception vException)
        {
            objLogger.LogDebug(vException, "Hugging Face licence probe failed for {ModelName}", aModelName);
            return null;
        }
    }

    private static string MapHttpError(HttpRequestException aException) => aException.StatusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            "The source rejected the download (401/403) — add or update the source API token, and accept the model licence on the source site if required.",
        HttpStatusCode.NotFound =>
            "The model file was not found (404) — check the model name/path against the source repository.",
        HttpStatusCode.TooManyRequests =>
            "The source rate-limited the download (429) — wait a few minutes and resume the install.",
        _ =>
            $"The download failed ({aException.StatusCode?.ToString() ?? "network error"}) — check the connection and resume the install; completed bytes are kept."
    };

    private static string ResolveTargetFilePath(ModelInstallItem aItem)
    {
        var vFileName = FileNameFromUrl(aItem.DownloadUrl);
        return Path.Combine(aItem.Destination, vFileName);
    }

    private static string FileNameFromUrl(string aUrl)
    {
        var vUri = new Uri(aUrl);
        var vName = Path.GetFileName(vUri.AbsolutePath);
        return string.IsNullOrWhiteSpace(vName) ? "model.bin" : vName;
    }

    private static long GetPartialLength(ModelInstallItem aItem)
    {
        var vPartialPath = ResolveTargetFilePath(aItem) + ".partial";
        return File.Exists(vPartialPath) ? new FileInfo(vPartialPath).Length : aItem.DownloadedBytes;
    }

    private void DeletePartialFile(ModelInstallItem aItem)
    {
        var vPartialPath = ResolveTargetFilePath(aItem) + ".partial";
        try
        {
            if (File.Exists(vPartialPath))
            {
                File.Delete(vPartialPath);
            }
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "Could not delete partial file {Path}", vPartialPath);
        }
    }

    /// <summary>
    /// Per-install runtime handle: the cancellation source and whether the cancel is a pause.
    /// </summary>
    private sealed class InstallRuntime
    {
        public CancellationTokenSource Cts { get; } = new();

        public bool IsPauseRequested { get; set; }
    }
}
