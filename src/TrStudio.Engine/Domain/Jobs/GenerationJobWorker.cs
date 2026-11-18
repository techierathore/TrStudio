using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Cost;
using TrStudio.Engine.Domain.Progress;
using TrStudio.Engine.Domain.Providers;
using TrStudio.Engine.Domain.Venice;
using TrStudio.Engine.Infrastructure.Jobs;
using TrStudio.Engine.Infrastructure.Observability;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Jobs;

/// <summary>
/// The worker executing one generation job, run by the in-app runner (phase 3, ADR-018): resolves the
/// provider, drives submit → poll → result, streams progress through
/// <see cref="IProgressNotifier"/>, honours user cancellation, and writes a complete
/// <c>"GenerationLog"</c> row for EVERY outcome — success and failure (REQ-FN-018). A failed
/// execution rethrows so the in-app runner retries it twice.
/// </summary>
public class GenerationJobWorker
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly IJobRepository objJobRepository;
    private readonly IRegistryRepository objRegistryRepository;
    private readonly IGenerationLogRepository objLogRepository;
    private readonly IAssetRepository objAssetRepository;
    private readonly IProviderResolver objProviderResolver;
    private readonly IProgressNotifier objProgressNotifier;
    private readonly ISpendLedger objSpendLedger;
    private readonly ISafetyPipeline objSafetyPipeline;
    private readonly IProvenanceStamper objProvenanceStamper;
    private readonly ILogger<GenerationJobWorker> objLogger;
    private readonly JobQueueStore? objQueueStore;

    /// <summary>
    /// Initialises the worker.
    /// </summary>
    /// <param name="aJobRepository">Job persistence.</param>
    /// <param name="aRegistryRepository">Registry data access (the model row may have been disabled since submit).</param>
    /// <param name="aLogRepository">Generation-log persistence.</param>
    /// <param name="aAssetRepository">Asset metadata persistence (post-check quarantine + provenance stamp).</param>
    /// <param name="aProviderResolver">Model → provider resolution.</param>
    /// <param name="aProgressNotifier">Progress publication seam feeding the SignalR hub.</param>
    /// <param name="aSpendLedger">Spend ledger; a billed job's actual cost is recorded on completion (REQ-FN-029).</param>
    /// <param name="aSafetyPipeline">Non-disableable safety post-check run on the OUTPUT (SAFE-01..06 / FN-056..063).</param>
    /// <param name="aProvenanceStamper">C2PA-style provenance stamper applied to every released output (SAFE-04 / FN-059).</param>
    /// <param name="aLogger">Structured logger.</param>
    public GenerationJobWorker(
        IJobRepository aJobRepository,
        IRegistryRepository aRegistryRepository,
        IGenerationLogRepository aLogRepository,
        IAssetRepository aAssetRepository,
        IProviderResolver aProviderResolver,
        IProgressNotifier aProgressNotifier,
        ISpendLedger aSpendLedger,
        ISafetyPipeline aSafetyPipeline,
        IProvenanceStamper aProvenanceStamper,
        ILogger<GenerationJobWorker> aLogger,
        JobQueueStore? aQueueStore = null)
    {
        objJobRepository = aJobRepository;
        objRegistryRepository = aRegistryRepository;
        objLogRepository = aLogRepository;
        objAssetRepository = aAssetRepository;
        objProviderResolver = aProviderResolver;
        objProgressNotifier = aProgressNotifier;
        objSpendLedger = aSpendLedger;
        objSafetyPipeline = aSafetyPipeline;
        objProvenanceStamper = aProvenanceStamper;
        objLogger = aLogger;
        objQueueStore = aQueueStore;
    }

    /// <summary>
    /// Executes a persisted generation job end-to-end on its resolved provider.
    /// </summary>
    /// <param name="aJobId">The job identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Signalled when the app closes.</param>
    /// <returns>A task completing when the job reaches a terminal state.</returns>
    /// <exception cref="InvalidOperationException">The provider reported failure; rethrown so the in-app runner retries.</exception>
    public Task ExecuteAsync(Guid aJobId, string aUserId, CancellationToken aCancellationToken)
        => ExecuteAsync(aJobId, aUserId, false, aCancellationToken);

    /// <summary>
    /// Executes one job; with <paramref name="aResume"/> a job queued at its provider before the app
    /// closed re-attaches and resumes polling instead of submitting again (phase 3, REQ-FN-094). A failed
    /// attempt throws so the in-app runner retries it twice, as the Hangfire policy did.
    /// </summary>
    /// <param name="aJobId">The job id.</param>
    /// <param name="aUserId">The owning user.</param>
    /// <param name="aResume">True to resume a provider-queued job.</param>
    /// <param name="aCancellationToken">Signalled when the app closes.</param>
    /// <returns>A task.</returns>
    public async Task ExecuteAsync(Guid aJobId, string aUserId, bool aResume, CancellationToken aCancellationToken)
    {
        // NFR-008: open a job-scoped trace span and structured-logging scope so every log line and
        // provider hop for this execution carries the same job-id correlation (submit → provider →
        // asset). No secrets are placed on the span or scope.
        using var vActivity = TrStudioTelemetry.ActivitySource.StartActivity("generation.job.execute");
        vActivity?.SetTag("trstudio.job_id", aJobId);
        using var vLogScope = objLogger.BeginScope(new Dictionary<string, object> { ["JobId"] = aJobId });

        var vJob = await objJobRepository.GetAsync(aJobId, aUserId, aCancellationToken).ConfigureAwait(false);
        if (vJob is null)
        {
            objLogger.LogWarning("Job {JobId} not found for user; skipping execution", aJobId);
            return;
        }

        if (vJob.State == JobState.Cancelled)
        {
            await WriteLogAsync(vJob, "cancelled", null, aCancellationToken).ConfigureAwait(false);
            return;
        }

        // Succeeded/Blocked are terminal here; a Failed row is NOT skipped — that is the
        // in-app runner retry path re-executing the job.
        if (vJob.State is JobState.Succeeded or JobState.Blocked)
        {
            return;
        }

        var vModel = await objRegistryRepository.GetAsync(vJob.ModelId, aCancellationToken).ConfigureAwait(false);
        if (vModel is null)
        {
            await FailAsync(vJob, $"Model '{vJob.ModelId}' was removed from the registry before the job ran.", aCancellationToken).ConfigureAwait(false);
            return;
        }

        // REQ-FN-074 (defence in depth): the orchestrator already rejects text-chat rows at submit, so
        // one should never reach the worker; guard anyway rather than fall into "no provider resolves".
        if (vModel.Task == GenerationTaskType.TextChat || vModel.ProviderKind == ProviderKind.TextLlm)
        {
            await FailAsync(vJob, $"Model '{vModel.Id}' is a text-chat model and cannot run generation jobs.", aCancellationToken).ConfigureAwait(false);
            return;
        }

        var vProvider = objProviderResolver.Resolve(vModel);

        if (aResume)
        {
            var vRef = objQueueStore is null ? null : await GetStoredRefAsync(aJobId, aCancellationToken).ConfigureAwait(false);
            if (vRef is not null && vProvider is IResumableGenerationProvider vResumable
                && await vResumable.ResumeAsync(vJob, vRef, aCancellationToken).ConfigureAwait(false))
            {
                objLogger.LogInformation("Job {JobId} resumed polling at its provider after a restart", aJobId);
                await PollAsync(vJob, aJobId, aUserId, vProvider, vModel, aCancellationToken).ConfigureAwait(false);
                return;
            }
        }

        vJob.State = JobState.Running;
        vJob.StartedUtc = DateTime.UtcNow;
        vJob.CompletedUtc = null;
        vJob.FailureMessage = null;
        vJob.RemediationHint = null;
        await objJobRepository.UpdateAsync(vJob, aUserId, aCancellationToken).ConfigureAwait(false);
        await NotifyAsync(vJob, aCancellationToken).ConfigureAwait(false);

        // NFR-008: the job id links submit, provider and asset; the provider hop is named on the span and in the log.
        vActivity?.SetTag("trstudio.provider", vModel.ProviderKind.ToString());
        vActivity?.SetTag("trstudio.model_id", vModel.Id);
        objLogger.LogInformation("Job {JobId} sent to provider {Provider} (model {ModelId})", aJobId, vModel.ProviderKind, vModel.Id);
        await vProvider.SubmitAsync(vJob, aCancellationToken).ConfigureAwait(false);

        // Phase 3 (ADR-018): keep the provider's own reference (Venice's queue id) on the row so the
        // job can resume after the app closes.
        if (objQueueStore is not null && vProvider is IResumableGenerationProvider vQueued
            && vQueued.GetProviderRef(aJobId) is { } vProviderRef)
        {
            await objQueueStore.SetProviderRefAsync(aJobId, vProviderRef, aCancellationToken).ConfigureAwait(false);
        }

        await PollAsync(vJob, aJobId, aUserId, vProvider, vModel, aCancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> GetStoredRefAsync(Guid aJobId, CancellationToken aCancellationToken)
    {
        var vTickets = await objQueueStore!.ListUnfinishedAsync(aCancellationToken).ConfigureAwait(false);
        return vTickets.FirstOrDefault(aTicket => aTicket.JobId == aJobId)?.ProviderRef;
    }

    private async Task PollAsync(GenerationJob vJob, Guid aJobId, string aUserId, TrStudio.Models.Providers.IGenerationProvider vProvider, RegistryModel vModel, CancellationToken aCancellationToken)
    {
        while (true)
        {
            await Task.Delay(PollInterval, aCancellationToken).ConfigureAwait(false);

            var vCurrent = await objJobRepository.GetAsync(aJobId, aUserId, aCancellationToken).ConfigureAwait(false);
            if (vCurrent is not null && vCurrent.State == JobState.Cancelled)
            {
                vJob.State = JobState.Cancelled;
                vJob.CompletedUtc = DateTime.UtcNow;
                await WriteLogAsync(vJob, "cancelled", null, aCancellationToken).ConfigureAwait(false);
                objLogger.LogInformation("Job {JobId} cancelled by the user during execution", aJobId);
                return;
            }

            var vStatus = await vProvider.GetStatusAsync(aJobId, aCancellationToken).ConfigureAwait(false);
            vJob.Progress = vStatus.Progress;
            vJob.Eta = vStatus.Eta;
            vJob.State = vStatus.State == JobState.Pending ? JobState.Running : vStatus.State;

            if (vStatus.State == JobState.Succeeded)
            {
                await CompleteAsync(vJob, vProvider, vModel, aCancellationToken).ConfigureAwait(false);
                return;
            }

            if (vStatus.State is JobState.Failed or JobState.Blocked)
            {
                await FailAsync(vJob, vStatus.FailureMessage ?? "The provider reported an unspecified failure.", aCancellationToken).ConfigureAwait(false);
                return;
            }

            await objJobRepository.UpdateStateAsync(aJobId, aUserId, vJob.State, vJob.Progress, null, aCancellationToken).ConfigureAwait(false);
            await NotifyAsync(vJob, aCancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CompleteAsync(GenerationJob aJob, TrStudio.Models.Providers.IGenerationProvider aProvider, RegistryModel aModel, CancellationToken aCancellationToken)
    {
        var vResult = await aProvider.GetResultAsync(aJob.JobId, aCancellationToken).ConfigureAwait(false);

        // FN-056..063 / SAFE-01..06: the non-disableable safety POST-CHECK runs on the OUTPUT of EVERY
        // provider before the asset is released. A blocked output is quarantined (deleted) and the job
        // is marked Blocked + audited; it is never surfaced. Then SAFE-04 provenance is stamped on the
        // released asset. Uncertain checks fail closed.
        var vAssets = await objAssetRepository.ListByJobAsync(aJob.JobId, aJob.UserId, aCancellationToken).ConfigureAwait(false);
        var vAsset = vAssets.FirstOrDefault(aAsset => aAsset.AssetId == vResult.AssetId) ?? vAssets.LastOrDefault();

        var vBytes = vAsset is not null && File.Exists(vAsset.StoragePath)
            ? await File.ReadAllBytesAsync(vAsset.StoragePath, aCancellationToken).ConfigureAwait(false)
            : null;

        var vSafetyContext = new SafetyEvaluationContext
        {
            JobId = aJob.JobId,
            UserId = aJob.UserId,
            ModelId = aJob.ModelId,
            Task = aJob.Task,
            Tier = aJob.Tier,
            ParamsJson = aJob.ParamsJson,
            SafetyClass = aModel.SafetyClass
        };

        var vPostCheck = await objSafetyPipeline.PostCheckAsync(
            vSafetyContext,
            new SafetyContentSample { Content = vBytes, MediaKind = vAsset?.Kind ?? "image" },
            aCancellationToken).ConfigureAwait(false);

        if (!vPostCheck.Allowed)
        {
            if (vAsset is not null)
            {
                await objAssetRepository.DeleteAsync(vAsset.AssetId, aJob.UserId, aCancellationToken).ConfigureAwait(false);
            }

            await BlockAsync(aJob, vPostCheck.Message, aCancellationToken).ConfigureAwait(false);
            return;
        }

        // SAFE-04 / FN-059: stamp the C2PA-style provenance manifest onto the released asset.
        if (vAsset is not null && vBytes is not null)
        {
            var vManifest = objProvenanceStamper.BuildManifest(vAsset, vBytes, vSafetyContext);
            await objAssetRepository.UpdateProvenanceAsync(vAsset.AssetId, aJob.UserId, vManifest, aCancellationToken).ConfigureAwait(false);
        }

        aJob.State = JobState.Succeeded;
        aJob.Progress = 100;
        aJob.Eta = TimeSpan.Zero;
        aJob.CompletedUtc = DateTime.UtcNow;
        aJob.ActualCost = aJob.CostEstimate;
        await objJobRepository.UpdateAsync(aJob, aJob.UserId, aCancellationToken).ConfigureAwait(false);
        await WriteLogAsync(aJob, "succeeded", null, aCancellationToken, VenicePriceSources.For(aModel.ProviderKind, aJob.Task)).ConfigureAwait(false);
        await NotifyAsync(aJob, aCancellationToken).ConfigureAwait(false);

        // REQ-FN-029/NFR-007: record the actual cost against the billing channel so month-to-date
        // spend accrues across RunPod AND closed-API channels. Local/free jobs are ignored inside
        // the ledger.
        await objSpendLedger.RecordAsync(
            aJob.UserId,
            aJob.JobId,
            aJob.ModelId,
            SpendChannels.FromProviderKind(aModel.ProviderKind),
            aJob.ActualCost ?? 0m,
            aCancellationToken).ConfigureAwait(false);

        objLogger.LogInformation("Job {JobId} succeeded; asset {AssetId} persisted", aJob.JobId, vResult.AssetId);
    }

    private async Task FailAsync(GenerationJob aJob, string aFailureMessage, CancellationToken aCancellationToken)
    {
        var vHint = RemediationHints.ForFailure(aFailureMessage);

        aJob.State = JobState.Failed;
        aJob.CompletedUtc = DateTime.UtcNow;
        aJob.FailureMessage = aFailureMessage;
        aJob.RemediationHint = vHint;
        await objJobRepository.UpdateAsync(aJob, aJob.UserId, aCancellationToken).ConfigureAwait(false);
        await WriteLogAsync(aJob, "failed", aFailureMessage, aCancellationToken).ConfigureAwait(false);
        await NotifyAsync(aJob, aCancellationToken).ConfigureAwait(false);
        objLogger.LogWarning("Job {JobId} failed: {FailureMessage}", aJob.JobId, aFailureMessage);

        // Rethrow so the in-app runner retries the job twice (phase 3, ADR-018; the old Hangfire policy).
        throw new InvalidOperationException(aFailureMessage);
    }

    /// <summary>
    /// Marks a job Blocked by the output post-check (SAFE-01..06). Unlike a failure this is TERMINAL
    /// and does NOT rethrow — a safety block must never be retried. The safety audit entry
    /// is written by the pipeline; a "blocked" generation-log row is written here for full traceability.
    /// </summary>
    private async Task BlockAsync(GenerationJob aJob, string aBlockMessage, CancellationToken aCancellationToken)
    {
        aJob.State = JobState.Blocked;
        aJob.Progress = 0;
        aJob.CompletedUtc = DateTime.UtcNow;
        aJob.FailureMessage = aBlockMessage;
        aJob.RemediationHint = "This output was refused by a non-disableable safety floor and cannot be retried as-is.";
        await objJobRepository.UpdateAsync(aJob, aJob.UserId, aCancellationToken).ConfigureAwait(false);
        await WriteLogAsync(aJob, "blocked", aBlockMessage, aCancellationToken).ConfigureAwait(false);
        await NotifyAsync(aJob, aCancellationToken).ConfigureAwait(false);
        objLogger.LogWarning("Job {JobId} BLOCKED at output post-check: {BlockMessage}", aJob.JobId, aBlockMessage);
    }

    private Task WriteLogAsync(GenerationJob aJob, string aOutcome, string? aFailureMessage, CancellationToken aCancellationToken, string? aPriceSource = null)
    {
        var vDurationMs = aJob.StartedUtc.HasValue && aJob.CompletedUtc.HasValue
            ? (long?)(aJob.CompletedUtc.Value - aJob.StartedUtc.Value).TotalMilliseconds
            : null;

        // NFR-008: emit the latency histogram + completion counter tagged with the terminal outcome
        // so error/blocked rates are queryable alongside latency.
        TrStudioTelemetry.RecordJobCompletion(aOutcome, vDurationMs);

        return objLogRepository.InsertAsync(new GenerationLogEntry
        {
            LogId = Guid.NewGuid(),
            JobId = aJob.JobId,
            UserId = aJob.UserId,
            ModelId = aJob.ModelId,
            Tier = aJob.Tier,
            ParamsJson = aJob.ParamsJson,
            CostEstimate = aJob.CostEstimate,
            ActualCost = aJob.ActualCost,
            DurationMs = vDurationMs,
            Outcome = aOutcome,
            FailureMessage = aFailureMessage,
            PriceSource = aJob.ActualCost is null ? null : aPriceSource,
            CreatedUtc = DateTime.UtcNow
        }, aCancellationToken);
    }

    private Task NotifyAsync(GenerationJob aJob, CancellationToken aCancellationToken)
        => objProgressNotifier.PublishAsync(new JobProgressUpdate
        {
            JobId = aJob.JobId,
            UserId = aJob.UserId,
            State = aJob.State,
            Progress = aJob.Progress,
            Eta = aJob.Eta,
            FailureMessage = aJob.FailureMessage,
            RemediationHint = aJob.RemediationHint
        }, aCancellationToken);
}
