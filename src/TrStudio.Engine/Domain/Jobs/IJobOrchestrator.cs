using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;

namespace TrStudio.Engine.Domain.Jobs;

/// <summary>
/// The generation-job orchestrator (REQ-FN-001/002/018): validates a submission against the
/// registry's declarative params schema, resolves the model to exactly one provider,
/// cost-estimates, persists the job and enqueues it on the durable Hangfire queue. The caller
/// drives every provider through the identical submit → poll → result → cancel sequence.
/// </summary>
public interface IJobOrchestrator
{
    /// <summary>
    /// Validates, cost-estimates, persists and enqueues a generation job.
    /// </summary>
    /// <param name="aRequest">The submission request.</param>
    /// <param name="aUserId">The owning AppManager user id (owner scoping, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the submission.</param>
    /// <returns>The accepted job id and pre-run cost estimate.</returns>
    /// <exception cref="KeyNotFoundException">The model is unknown, disabled or hidden by a disabled provider.</exception>
    /// <exception cref="ArgumentException">The request violates the model's params schema or task declaration.</exception>
    Task<JobSubmitResponse> SubmitAsync(JobSubmitRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Gets the current status of a job, owner-filtered.
    /// </summary>
    /// <param name="aJobId">The job identifier.</param>
    /// <param name="aUserId">The owning AppManager user id.</param>
    /// <param name="aCancellationToken">Token to cancel the query.</param>
    /// <returns>The status snapshot, or null when the job is not found or not owned by the caller.</returns>
    Task<JobStatusResponse?> GetStatusAsync(Guid aJobId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists the caller's jobs newest-first, paginated.
    /// </summary>
    /// <param name="aUserId">The owning AppManager user id.</param>
    /// <param name="aPage">The 1-based page number.</param>
    /// <param name="aPageSize">The page size.</param>
    /// <param name="aCancellationToken">Token to cancel the query.</param>
    /// <returns>A page of the caller's jobs as status snapshots.</returns>
    Task<PagedResult<JobStatusResponse>> ListAsync(string aUserId, int aPage, int aPageSize, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Gets the output assets of a completed job, owner-filtered.
    /// </summary>
    /// <param name="aJobId">The job identifier.</param>
    /// <param name="aUserId">The owning AppManager user id.</param>
    /// <param name="aCancellationToken">Token to cancel the query.</param>
    /// <returns>The job's assets, or null when the job is not found; empty when not yet completed.</returns>
    Task<IReadOnlyList<GeneratedAsset>?> GetResultAsync(Guid aJobId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Requests cancellation of a queued/running job, owner-filtered.
    /// </summary>
    /// <param name="aJobId">The job identifier.</param>
    /// <param name="aUserId">The owning AppManager user id.</param>
    /// <param name="aCancellationToken">Token to cancel the request itself.</param>
    /// <returns><c>true</c> when the job was cancelled; <c>false</c> when it was already terminal; null when not found.</returns>
    Task<bool?> CancelAsync(Guid aJobId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Re-submits a job from a generation-log entry (one-click reproduce, REQ-FN-018).
    /// </summary>
    /// <param name="aLogId">The generation-log entry identifier.</param>
    /// <param name="aUserId">The owning AppManager user id.</param>
    /// <param name="aCancellationToken">Token to cancel the submission.</param>
    /// <returns>The new job's submit response, or null when the log entry is not found or not owned by the caller.</returns>
    Task<JobSubmitResponse?> ReproduceAsync(Guid aLogId, string aUserId, CancellationToken aCancellationToken = default);
}
