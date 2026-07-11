using System.Text.Json;
using Hangfire;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Cost;
using TrStudio.Engine.Domain.Progress;
using TrStudio.Engine.Domain.Providers;
using TrStudio.Engine.Domain.Registry;
using TrStudio.Engine.Domain.Safety;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Jobs;

/// <summary>
/// Default <see cref="IJobOrchestrator"/>. Resolves modelId → registry row → exactly one
/// provider, validates the request against the declarative params schema, estimates cost,
/// persists the job and enqueues <see cref="GenerationJobWorker"/> on the durable Hangfire
/// queue — the identical sequence for every provider (REQ-FN-001/002).
/// </summary>
public class JobOrchestrator : IJobOrchestrator
{
    private readonly IJobRepository objJobRepository;
    private readonly IAssetRepository objAssetRepository;
    private readonly IGenerationLogRepository objLogRepository;
    private readonly IModelCatalogService objModelCatalog;
    private readonly IProviderResolver objProviderResolver;
    private readonly IBackgroundJobClient objBackgroundJobClient;
    private readonly IProgressNotifier objProgressNotifier;
    private readonly ICostEstimator objCostEstimator;
    private readonly ISpendLedger objSpendLedger;
    private readonly ISafetyPipeline objSafetyPipeline;
    private readonly CostOptions objCostOptions;
    private readonly ILogger<JobOrchestrator> objLogger;

    /// <summary>
    /// Initialises the orchestrator.
    /// </summary>
    /// <param name="aJobRepository">Job persistence.</param>
    /// <param name="aAssetRepository">Asset metadata persistence.</param>
    /// <param name="aLogRepository">Generation-log persistence.</param>
    /// <param name="aModelCatalog">The creator-visible model catalog.</param>
    /// <param name="aProviderResolver">Model → provider resolution.</param>
    /// <param name="aBackgroundJobClient">The Hangfire enqueue client.</param>
    /// <param name="aProgressNotifier">Progress publication seam.</param>
    /// <param name="aCostEstimator">Per-job cost estimator over the registry cost block (REQ-FN-029).</param>
    /// <param name="aSpendLedger">Monthly spend-cap guardrail (REQ-FN-029/NFR-007).</param>
    /// <param name="aSafetyPipeline">Non-disableable safety pre-check run BEFORE enqueue (SAFE-01..06 / FN-056..063).</param>
    /// <param name="aCostOptions">Cost guardrail options (whether over-cap submits are blocked).</param>
    /// <param name="aLogger">Structured logger (never logs params content verbatim with secrets).</param>
    public JobOrchestrator(
        IJobRepository aJobRepository,
        IAssetRepository aAssetRepository,
        IGenerationLogRepository aLogRepository,
        IModelCatalogService aModelCatalog,
        IProviderResolver aProviderResolver,
        IBackgroundJobClient aBackgroundJobClient,
        IProgressNotifier aProgressNotifier,
        ICostEstimator aCostEstimator,
        ISpendLedger aSpendLedger,
        ISafetyPipeline aSafetyPipeline,
        CostOptions aCostOptions,
        ILogger<JobOrchestrator> aLogger)
    {
        objJobRepository = aJobRepository;
        objAssetRepository = aAssetRepository;
        objLogRepository = aLogRepository;
        objModelCatalog = aModelCatalog;
        objProviderResolver = aProviderResolver;
        objBackgroundJobClient = aBackgroundJobClient;
        objProgressNotifier = aProgressNotifier;
        objCostEstimator = aCostEstimator;
        objSpendLedger = aSpendLedger;
        objSafetyPipeline = aSafetyPipeline;
        objCostOptions = aCostOptions;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<JobSubmitResponse> SubmitAsync(JobSubmitRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vModel = await objModelCatalog.GetVisibleAsync(aRequest.ModelId, aCancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Model '{aRequest.ModelId}' is not available.");

        // REQ-FN-074: a text-chat LLM row is served synchronously by PromptChatService, deliberately
        // outside the generation-job spine — it is never resolved to a generation provider. Reject it
        // here with an actionable message (mapped to HTTP 400 by the spine endpoint) before resolution.
        if (vModel.Task == GenerationTaskType.TextChat || vModel.ProviderKind == ProviderKind.TextLlm)
        {
            throw new ArgumentException($"Model '{vModel.Id}' is a text-chat model and cannot run generation jobs.");
        }

        if (vModel.Task != aRequest.Task)
        {
            throw new ArgumentException($"Model '{vModel.Id}' performs {vModel.Task}, not {aRequest.Task}.");
        }

        ValidateParams(vModel, aRequest.ParamsJson);

        var vProvider = objProviderResolver.Resolve(vModel);

        var vJob = new GenerationJob
        {
            JobId = Guid.NewGuid(),
            UserId = aUserId,
            ModelId = vModel.Id,
            Task = aRequest.Task,
            Tier = aRequest.Tier,
            ParamsJson = string.IsNullOrWhiteSpace(aRequest.ParamsJson) ? "{}" : aRequest.ParamsJson,
            State = JobState.Pending,
            Progress = 0,
            CreatedUtc = DateTime.UtcNow,
            ProjectId = aRequest.ProjectId,
            SeedUsed = aRequest.Seed ?? Random.Shared.NextInt64(0, long.MaxValue)
        };

        // FN-056..063 / SAFE-01..06: the non-disableable safety pre-check runs on the INPUT BEFORE the
        // job is enqueued, on EVERY provider. A blocked submission is persisted as Blocked, written to
        // the generation log and audited (inside the pipeline), then refused with a 4xx — it never
        // reaches the queue. The floors are enforced regardless of any mode (incl. NSFW). Fail closed.
        var vSafetyContext = new SafetyEvaluationContext
        {
            JobId = vJob.JobId,
            UserId = aUserId,
            ModelId = vModel.Id,
            Task = aRequest.Task,
            Tier = aRequest.Tier,
            ParamsJson = vJob.ParamsJson,
            SafetyClass = vModel.SafetyClass
        };

        var vSafety = await objSafetyPipeline.PreCheckAsync(vSafetyContext, aCancellationToken).ConfigureAwait(false);
        if (!vSafety.Allowed)
        {
            vJob.State = JobState.Blocked;
            vJob.Progress = 0;
            vJob.FailureMessage = vSafety.Message;
            vJob.RemediationHint = "This request was refused by a non-disableable safety floor and cannot be retried as-is.";
            vJob.CompletedUtc = DateTime.UtcNow;
            await objJobRepository.InsertAsync(vJob, aCancellationToken).ConfigureAwait(false);
            await WriteBlockedLogAsync(vJob, vSafety.Message, aCancellationToken).ConfigureAwait(false);
            objLogger.LogWarning(
                "Job for model {ModelId} by user {UserId} BLOCKED at pre-check by floor {Floor}",
                vModel.Id, aUserId, vSafety.Floor);
            throw new SafetyBlockedException(vSafety.Floor, vSafety.Message);
        }

        // REQ-FN-029/NFR-007: estimate the job's cost from the registry cost block BEFORE it runs,
        // then check it against the monthly spend cap (a billed submit that would exceed the cap is
        // blocked with an actionable message; one that merely nears the cap is warned).
        vJob.CostEstimate = await objCostEstimator.EstimateAsync(vJob, aCancellationToken).ConfigureAwait(false);

        var vGuard = await objSpendLedger.EvaluateAsync(vJob.CostEstimate ?? 0m, aCancellationToken).ConfigureAwait(false);
        if (vGuard.WouldExceed && objCostOptions.BlockOverCap)
        {
            objLogger.LogWarning(
                "Job for model {ModelId} by user {UserId} blocked at submit: over the monthly spend cap ({Projected}/{Cap})",
                vModel.Id, aUserId, vGuard.Projected, vGuard.Cap);
            throw new SpendCapExceededException(vGuard.Message ?? "The monthly spend cap would be exceeded by this job.");
        }

        await objJobRepository.InsertAsync(vJob, aCancellationToken).ConfigureAwait(false);

        objBackgroundJobClient.Enqueue<GenerationJobWorker>(
            aWorker => aWorker.ExecuteAsync(vJob.JobId, aUserId, CancellationToken.None));

        await objJobRepository.UpdateStateAsync(vJob.JobId, aUserId, JobState.Queued, 0, null, aCancellationToken).ConfigureAwait(false);
        objLogger.LogInformation("Job {JobId} queued for model {ModelId} by user {UserId}", vJob.JobId, vModel.Id, aUserId);

        // Surface an over-cap warning (when not hard-blocking) or a near-cap heads-up to the UI.
        return new JobSubmitResponse
        {
            JobId = vJob.JobId,
            CostEstimate = vJob.CostEstimate,
            Warning = vGuard.WouldExceed || vGuard.ShouldWarn ? vGuard.Message : null
        };
    }

    /// <inheritdoc />
    public async Task<JobStatusResponse?> GetStatusAsync(Guid aJobId, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vJob = await objJobRepository.GetAsync(aJobId, aUserId, aCancellationToken).ConfigureAwait(false);
        return vJob is null ? null : ToStatusResponse(vJob);
    }

    /// <inheritdoc />
    public async Task<PagedResult<JobStatusResponse>> ListAsync(string aUserId, int aPage, int aPageSize, CancellationToken aCancellationToken = default)
    {
        var vPage = await objJobRepository.ListAsync(aUserId, aPage, aPageSize, aCancellationToken).ConfigureAwait(false);
        return new PagedResult<JobStatusResponse>
        {
            Items = vPage.Items.Select(ToStatusResponse).ToList(),
            TotalCount = vPage.TotalCount,
            Page = vPage.Page,
            PageSize = vPage.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GeneratedAsset>?> GetResultAsync(Guid aJobId, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vJob = await objJobRepository.GetAsync(aJobId, aUserId, aCancellationToken).ConfigureAwait(false);
        if (vJob is null)
        {
            return null;
        }

        return await objAssetRepository.ListByJobAsync(aJobId, aUserId, aCancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool?> CancelAsync(Guid aJobId, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vJob = await objJobRepository.GetAsync(aJobId, aUserId, aCancellationToken).ConfigureAwait(false);
        if (vJob is null)
        {
            return null;
        }

        if (vJob.State is JobState.Succeeded or JobState.Failed or JobState.Cancelled or JobState.Blocked)
        {
            return false;
        }

        await objJobRepository.UpdateStateAsync(aJobId, aUserId, JobState.Cancelled, vJob.Progress, "Cancelled by the user.", aCancellationToken).ConfigureAwait(false);
        await objProgressNotifier.PublishAsync(new JobProgressUpdate
        {
            JobId = aJobId,
            UserId = aUserId,
            State = JobState.Cancelled,
            Progress = vJob.Progress,
            FailureMessage = "Cancelled by the user."
        }, aCancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<JobSubmitResponse?> ReproduceAsync(Guid aLogId, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vEntry = await objLogRepository.GetAsync(aLogId, aUserId, aCancellationToken).ConfigureAwait(false);
        if (vEntry is null)
        {
            return null;
        }

        var vModel = await objModelCatalog.GetVisibleAsync(vEntry.ModelId, aCancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Model '{vEntry.ModelId}' is no longer available to reproduce this job.");

        var vRequest = new JobSubmitRequest
        {
            ModelId = vEntry.ModelId,
            Task = vModel.Task,
            Tier = vEntry.Tier,
            ParamsJson = vEntry.ParamsJson
        };

        return await SubmitAsync(vRequest, aUserId, aCancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a "blocked" generation-log row so a safety-refused submission is fully traceable in
    /// the audit trail (SAFE-06), alongside the safety audit entry the pipeline records.
    /// </summary>
    private Task WriteBlockedLogAsync(GenerationJob aJob, string aFailureMessage, CancellationToken aCancellationToken)
        => objLogRepository.InsertAsync(new GenerationLogEntry
        {
            LogId = Guid.NewGuid(),
            JobId = aJob.JobId,
            UserId = aJob.UserId,
            ModelId = aJob.ModelId,
            Tier = aJob.Tier,
            ParamsJson = aJob.ParamsJson,
            CostEstimate = null,
            ActualCost = null,
            DurationMs = null,
            Outcome = "blocked",
            FailureMessage = aFailureMessage,
            CreatedUtc = DateTime.UtcNow
        }, aCancellationToken);

    private static void ValidateParams(RegistryModel aModel, string aParamsJson)
    {
        JsonDocument vDocument;
        try
        {
            vDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(aParamsJson) ? "{}" : aParamsJson);
        }
        catch (JsonException vException)
        {
            throw new ArgumentException("ParamsJson is not valid JSON.", vException);
        }

        using (vDocument)
        {
            if (vDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("ParamsJson must be a JSON object.");
            }

            foreach (var vSpec in aModel.Params.Where(aSpec => aSpec.Required))
            {
                if (!vDocument.RootElement.TryGetProperty(vSpec.Key, out var vValue) || vValue.ValueKind == JsonValueKind.Null)
                {
                    throw new ArgumentException($"Required parameter '{vSpec.Key}' is missing.");
                }
            }
        }
    }

    private static JobStatusResponse ToStatusResponse(GenerationJob aJob) => new()
    {
        JobId = aJob.JobId,
        State = aJob.State,
        Progress = aJob.Progress,
        Eta = aJob.Eta,
        FailureMessage = aJob.FailureMessage,
        RemediationHint = aJob.RemediationHint,
        CostEstimate = aJob.CostEstimate,
        ActualCost = aJob.ActualCost
    };
}
