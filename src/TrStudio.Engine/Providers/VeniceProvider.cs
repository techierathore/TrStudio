using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Venice;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Providers;

/// <summary>
/// Venice cloud <see cref="IGenerationProvider"/> for pictures and video (phase 2,
/// ADR-014). It claims only registry rows whose <see cref="ProviderKind"/> is Venice, and only
/// <see cref="SupportedTasks"/> — never Inpaint, BackgroundRemove, LipSync, TextToSpeech or
/// WorkflowRun. Address and key are read from the database on every call (BRD-98). Pictures run
/// inside <see cref="SubmitAsync"/>; a clip is queued there and polled through
/// <see cref="GetStatusAsync"/> (the worker turns each poll into SignalR progress). A failure keeps
/// Venice's own text: "Venice: …" (BRD-119).
/// </summary>
public class VeniceProvider : IGenerationProvider, TrStudio.Engine.Domain.Jobs.IResumableGenerationProvider
{
    /// <summary>The tasks Venice serves.</summary>
    public static readonly IReadOnlySet<GenerationTaskType> SupportedTasks = new HashSet<GenerationTaskType>
    {
        GenerationTaskType.TextToImage,
        GenerationTaskType.ImageToImage,
        GenerationTaskType.Upscale,
        GenerationTaskType.TextToVideo,
        GenerationTaskType.ImageToVideo
    };

    private readonly IRegistryRepository objRegistryRepository;
    private readonly IVeniceConnectionReader objConnectionReader;
    private readonly VeniceClient objClient;
    private readonly VeniceAssetIo objAssetIo;
    private readonly ISafetyPolicyService objSafetyPolicy;
    private readonly ILogger<VeniceProvider> objLogger;
    private readonly ConcurrentDictionary<Guid, VeniceExecution> objExecutions = new();

    /// <summary>
    /// Initialises the provider.
    /// </summary>
    /// <param name="aRegistryRepository">Registry rows (the Venice model id and ability flags).</param>
    /// <param name="aConnectionReader">Reads Venice's address and key from the database.</param>
    /// <param name="aClient">The typed Venice client.</param>
    /// <param name="aAssetIo">Reads input pictures and stores outputs.</param>
    /// <param name="aSafetyPolicy">The owner's content policy (unrestricted mode turns safe mode off).</param>
    /// <param name="aLogger">Structured logger (never logs the key).</param>
    public VeniceProvider(
        IRegistryRepository aRegistryRepository,
        IVeniceConnectionReader aConnectionReader,
        VeniceClient aClient,
        VeniceAssetIo aAssetIo,
        ISafetyPolicyService aSafetyPolicy,
        ILogger<VeniceProvider> aLogger)
    {
        objRegistryRepository = aRegistryRepository;
        objConnectionReader = aConnectionReader;
        objClient = aClient;
        objAssetIo = aAssetIo;
        objSafetyPolicy = aSafetyPolicy;
        objLogger = aLogger;
    }

    /// <summary>How often a queued clip is polled at Venice (the worker polls this provider more often).</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public bool Supports(GenerationTaskType aTask, RegistryModel aModel)
        => aModel.ProviderKind == ProviderKind.Venice && SupportedTasks.Contains(aTask);

    /// <inheritdoc />
    public async Task<Guid> SubmitAsync(GenerationJob aJob, CancellationToken aCancellationToken)
    {
        var vExecution = new VeniceExecution(aJob);
        objExecutions[aJob.JobId] = vExecution;

        try
        {
            var vModel = await GetModelAsync(aJob, aCancellationToken).ConfigureAwait(false);
            var vConnection = await objConnectionReader.ReadAsync(aCancellationToken).ConfigureAwait(false);
            var vParams = VeniceJobParams.Parse(aJob.ParamsJson);
            vExecution.Connection = vConnection;
            vExecution.ProviderModelId = vModel.ProviderModelId;

            if (VenicePriceSources.IsVideo(aJob.Task))
            {
                await QueueClipAsync(aJob, vModel, vParams, vExecution, aCancellationToken).ConfigureAwait(false);
            }
            else
            {
                var vBytes = await RunPictureAsync(aJob, vModel, vParams, vConnection, aCancellationToken).ConfigureAwait(false);
                vExecution.Result = await objAssetIo.SaveAsync(aJob, vBytes, "image", aCancellationToken).ConfigureAwait(false);
                vExecution.Status = new JobStatus(JobState.Succeeded, 100, TimeSpan.Zero, null, null);
            }
        }
        catch (Exception vException) when (vException is not OperationCanceledException)
        {
            Fail(vExecution, vException);
        }

        return aJob.JobId;
    }

    /// <inheritdoc />
    public string? GetProviderRef(Guid aJobId)
        => objExecutions.TryGetValue(aJobId, out var vExecution) ? vExecution.QueueId : null;

    /// <inheritdoc />
    public async Task<bool> ResumeAsync(GenerationJob aJob, string aProviderRef, CancellationToken aCancellationToken)
    {
        if (!VenicePriceSources.IsVideo(aJob.Task) || string.IsNullOrWhiteSpace(aProviderRef))
        {
            return false;
        }

        try
        {
            var vModel = await GetModelAsync(aJob, aCancellationToken).ConfigureAwait(false);
            var vConnection = await objConnectionReader.ReadAsync(aCancellationToken).ConfigureAwait(false);
            objExecutions[aJob.JobId] = new VeniceExecution(aJob)
            {
                Connection = vConnection,
                ProviderModelId = vModel.ProviderModelId,
                QueueId = aProviderRef,
                LastPollUtc = DateTime.MinValue,
                Status = new JobStatus(JobState.Running, Math.Max(1, aJob.Progress), null, null, null)
            };
            objLogger.LogInformation("Venice clip for job {JobId} resumed (queue {QueueId})", aJob.JobId, aProviderRef);
            return true;
        }
        catch (Exception vException) when (vException is not OperationCanceledException)
        {
            objLogger.LogWarning("Venice clip for job {JobId} could not resume: {Message}", aJob.JobId, vException.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<JobStatus> GetStatusAsync(Guid aJobId, CancellationToken aCancellationToken)
    {
        if (!objExecutions.TryGetValue(aJobId, out var vExecution))
        {
            throw new InvalidOperationException($"Job '{aJobId}' was not submitted to this provider instance.");
        }

        if (vExecution.Status.State != JobState.Running || vExecution.QueueId is null
            || DateTime.UtcNow - vExecution.LastPollUtc < PollInterval)
        {
            return vExecution.Status;
        }

        vExecution.LastPollUtc = DateTime.UtcNow;
        try
        {
            var vRetrieved = await objClient.RetrieveVideoAsync(vExecution.Connection!, vExecution.ProviderModelId!, vExecution.QueueId, aCancellationToken).ConfigureAwait(false);
            if (!vRetrieved.Done && vRetrieved.Video is null)
            {
                vExecution.Status = new JobStatus(JobState.Running, vRetrieved.Progress, vRetrieved.Eta, null, null);
                return vExecution.Status;
            }

            var vBytes = vRetrieved.Video;
            var vUrl = vRetrieved.DownloadUrl ?? vExecution.DownloadUrl;
            if (vBytes is null && vUrl is not null)
            {
                vBytes = await objClient.DownloadAsync(vUrl, aCancellationToken).ConfigureAwait(false);
            }

            if (vBytes is null)
            {
                throw new VeniceApiException(200, $"{VeniceApiException.Prefix}the clip finished but no video was returned (HTTP 200)");
            }

            vExecution.Result = await objAssetIo.SaveAsync(vExecution.Job, vBytes, "video", aCancellationToken).ConfigureAwait(false);
            vExecution.Status = new JobStatus(JobState.Succeeded, 100, TimeSpan.Zero, null, null);
            await TryCompleteAsync(vExecution, aCancellationToken).ConfigureAwait(false);
        }
        catch (Exception vException) when (vException is not OperationCanceledException)
        {
            Fail(vExecution, vException);
        }

        return vExecution.Status;
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

    /// <summary>
    /// Venice's own stated price for the job (owner decision 1A), read BEFORE anything is sent so
    /// the monthly cap can refuse it: a clip's <c>/video/quote</c> answer, or a picture's listed price
    /// from <c>GET /models</c> for the chosen resolution. Neither call carries the prompt.
    /// </summary>
    /// <param name="aJob">The job.</param>
    /// <param name="aCancellationToken">Token to cancel the price read.</param>
    /// <returns>The US-dollar price; zero when the list states none.</returns>
    /// <exception cref="VeniceApiException">Venice refused the price call.</exception>
    public async Task<decimal> EstimateCostAsync(GenerationJob aJob, CancellationToken aCancellationToken)
    {
        var vModel = await GetModelAsync(aJob, aCancellationToken).ConfigureAwait(false);
        var vParams = VeniceJobParams.Parse(aJob.ParamsJson);
        var vConnection = await objConnectionReader.ReadAsync(aCancellationToken).ConfigureAwait(false);

        if (VenicePriceSources.IsVideo(aJob.Task))
        {
            return await objClient.QuoteVideoAsync(vConnection, VeniceRequestBuilder.VideoQuote(vModel, vParams), aCancellationToken).ConfigureAwait(false);
        }

        var vList = await objClient.ListModelsAsync(vConnection.BaseUrl, VenicePriceSources.ListTypeFor(aJob.Task), null, aCancellationToken).ConfigureAwait(false);
        var vEntry = vList.FirstOrDefault(aEntry => string.Equals(aEntry.Id, vModel.ProviderModelId, StringComparison.OrdinalIgnoreCase));
        var vPrice = vEntry?.ListedPrice(aJob.Task, vParams.Resolution, vParams.Scale);
        if (vPrice is null)
        {
            objLogger.LogWarning("Venice lists no price for model {ProviderModelId}; recording 0", vModel.ProviderModelId);
        }

        return vPrice ?? 0m;
    }

    private async Task<byte[]> RunPictureAsync(GenerationJob aJob, RegistryModel aModel, VeniceJobParams aParams, VeniceConnection aConnection, CancellationToken aCancellationToken)
    {
        var vSafeMode = await SafeModeAsync(aCancellationToken).ConfigureAwait(false);
        switch (aJob.Task)
        {
            case GenerationTaskType.TextToImage:
            {
                var vRefs = await objAssetIo.LoadAsync(aParams.ReferenceAssetIds, aJob.UserId, aCancellationToken).ConfigureAwait(false);
                var vBody = VeniceRequestBuilder.TextToImage(aModel, aParams, aJob.SeedUsed ?? 0, vSafeMode, vRefs.Select(aRef => aRef.DataUrl).ToList());
                return await objClient.GenerateImageAsync(aConnection, vBody, aCancellationToken).ConfigureAwait(false);
            }

            case GenerationTaskType.ImageToImage:
            {
                var vSources = await objAssetIo.LoadAsync(aParams.SourceAssetIds.Concat(aParams.ReferenceAssetIds), aJob.UserId, aCancellationToken).ConfigureAwait(false);
                var (vBody, vMulti) = VeniceRequestBuilder.ImageToImage(aModel, aParams, vSafeMode, vSources.Select(aSource => aSource.DataUrl).ToList());
                return await objClient.EditImageAsync(aConnection, vBody, vMulti, aCancellationToken).ConfigureAwait(false);
            }

            case GenerationTaskType.Upscale:
            {
                var vSources = await objAssetIo.LoadAsync(aParams.SourceAssetIds.Take(1), aJob.UserId, aCancellationToken).ConfigureAwait(false);
                if (vSources.Count == 0)
                {
                    throw new InvalidOperationException("Upscale needs a source picture (sourceAssetId).");
                }

                return await objClient.UpscaleImageAsync(aConnection, VeniceRequestBuilder.Upscale(aParams, vSources[0].Base64), aCancellationToken).ConfigureAwait(false);
            }

            default:
                throw new InvalidOperationException($"Venice does not run {aJob.Task}.");
        }
    }

    private async Task QueueClipAsync(GenerationJob aJob, RegistryModel aModel, VeniceJobParams aParams, VeniceExecution aExecution, CancellationToken aCancellationToken)
    {
        string? vImageUrl = null;
        if (aJob.Task == GenerationTaskType.ImageToVideo && aParams.SourceAssetIds.Count > 0)
        {
            vImageUrl = (await objAssetIo.LoadAsync(aParams.SourceAssetIds.Take(1), aJob.UserId, aCancellationToken).ConfigureAwait(false))[0].DataUrl;
        }

        IReadOnlyList<string> vReferences = Array.Empty<string>();
        if (aModel.KeepsFixedCharacter && aParams.CharacterId is { } vCharacterId)
        {
            vReferences = await objAssetIo.LoadCharacterReferencesAsync(vCharacterId, aJob.UserId, aCancellationToken).ConfigureAwait(false);
        }

        var vExplicit = await LoadReferencesAsync(aJob, aParams, aCancellationToken).ConfigureAwait(false);
        var vBody = VeniceRequestBuilder.VideoQueue(aModel, aParams, vImageUrl, vReferences, vExplicit);
        var vQueued = await objClient.QueueVideoAsync(aExecution.Connection!, vBody, aCancellationToken).ConfigureAwait(false);
        aExecution.QueueId = vQueued.QueueId;
        aExecution.DownloadUrl = vQueued.DownloadUrl;
        aExecution.LastPollUtc = DateTime.MinValue;
        aExecution.Status = new JobStatus(JobState.Running, 1, null, null, null);
        objLogger.LogInformation("Venice clip queued for job {JobId} (queue {QueueId})", aJob.JobId, vQueued.QueueId);
    }

    /// <summary>
    /// The reference media a job names (a twin's pictures, clip and spoken line; the clip being
    /// extended; the end picture of a shot), loaded owner-scoped and encoded as data URLs.
    /// </summary>
    private async Task<VideoReferences> LoadReferencesAsync(GenerationJob aJob, VeniceJobParams aParams, CancellationToken aCancellationToken)
    {
        if (aParams.ReferenceImageAssetIds.Count == 0 && aParams.ReferenceVideoAssetIds.Count == 0
            && aParams.ReferenceAudioAssetIds.Count == 0 && aParams.EndImageAssetId is null)
        {
            return VideoReferences.None;
        }

        var vImages = await objAssetIo.LoadAsync(aParams.ReferenceImageAssetIds, aJob.UserId, aCancellationToken).ConfigureAwait(false);
        var vVideos = await objAssetIo.LoadAsync(aParams.ReferenceVideoAssetIds, aJob.UserId, aCancellationToken).ConfigureAwait(false);
        var vAudio = await objAssetIo.LoadAsync(aParams.ReferenceAudioAssetIds, aJob.UserId, aCancellationToken).ConfigureAwait(false);
        string? vEnd = null;
        if (aParams.EndImageAssetId is { } vEndId)
        {
            vEnd = (await objAssetIo.LoadAsync(new[] { vEndId }, aJob.UserId, aCancellationToken).ConfigureAwait(false)).FirstOrDefault().DataUrl;
        }

        return new VideoReferences(
            vImages.Select(aItem => aItem.DataUrl).ToList(),
            vVideos.Select(aItem => aItem.DataUrl).ToList(),
            vAudio.Select(aItem => aItem.DataUrl).ToList(),
            vEnd);
    }

    private async Task TryCompleteAsync(VeniceExecution aExecution, CancellationToken aCancellationToken)
    {
        try
        {
            await objClient.CompleteVideoAsync(aExecution.Connection!, aExecution.ProviderModelId!, aExecution.QueueId!, aCancellationToken).ConfigureAwait(false);
        }
        catch (VeniceApiException vException)
        {
            // The clip is already stored; failing to free Venice's copy must not fail the job.
            objLogger.LogWarning("Venice /video/complete failed for job {JobId}: {Message}", aExecution.Job.JobId, vException.Message);
        }
    }

    private async Task<bool> SafeModeAsync(CancellationToken aCancellationToken)
    {
        var vPolicy = await objSafetyPolicy.GetNsfwPolicyAsync(aCancellationToken).ConfigureAwait(false);
        return !vPolicy.EffectiveNsfwEnabled;
    }

    private async Task<RegistryModel> GetModelAsync(GenerationJob aJob, CancellationToken aCancellationToken)
        => await objRegistryRepository.GetAsync(aJob.ModelId, aCancellationToken).ConfigureAwait(false)
           ?? throw new InvalidOperationException($"Model '{aJob.ModelId}' is not in the registry.");

    private void Fail(VeniceExecution aExecution, Exception aException)
    {
        var vHint = aException is VeniceApiException vVenice
            ? vVenice.RemediationHint
            : "Check Venice's address, key and the model's Venice id on Registry & providers.";
        aExecution.Status = new JobStatus(JobState.Failed, aExecution.Status.Progress, null, aException.Message, vHint);
        objLogger.LogWarning("Venice job {JobId} failed: {Message}", aExecution.Job.JobId, aException.Message);
    }

    /// <summary>Mutable in-memory record for one submitted Venice job.</summary>
    private sealed class VeniceExecution
    {
        public VeniceExecution(GenerationJob aJob) => Job = aJob;

        public GenerationJob Job { get; }
        public JobStatus Status { get; set; } = new(JobState.Running, 0, null, null, null);
        public AssetReference? Result { get; set; }
        public VeniceConnection? Connection { get; set; }
        public string? ProviderModelId { get; set; }
        public string? QueueId { get; set; }
        public string? DownloadUrl { get; set; }
        public DateTime LastPollUtc { get; set; }
    }
}
