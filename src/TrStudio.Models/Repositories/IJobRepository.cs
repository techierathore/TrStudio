using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for generation jobs. Every read/write of user-owned rows takes the owning
/// AppManager user id and applies the owner filter server-side (BRD-81).
/// </summary>
public interface IJobRepository
{
    /// <summary>
    /// Inserts a new job row. The job's <see cref="GenerationJob.UserId"/> records ownership.
    /// </summary>
    /// <param name="aJob">The job to insert.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertAsync(GenerationJob aJob, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Gets a job by id, owner-filtered.
    /// </summary>
    /// <param name="aJobId">The job identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The job, or null when not found or not owned by <paramref name="aUserId"/>.</returns>
    Task<GenerationJob?> GetAsync(Guid aJobId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists the user's jobs newest-first, paginated.
    /// </summary>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aPage">The 1-based page number.</param>
    /// <param name="aPageSize">The page size.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A page of the user's jobs.</returns>
    Task<PagedResult<GenerationJob>> ListAsync(string aUserId, int aPage, int aPageSize, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Updates all mutable fields of a job, owner-filtered.
    /// </summary>
    /// <param name="aJob">The job carrying updated values.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is updated.</returns>
    Task UpdateAsync(GenerationJob aJob, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Updates only the lifecycle/progress fields of a job, owner-filtered.
    /// </summary>
    /// <param name="aJobId">The job identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aState">The new lifecycle state.</param>
    /// <param name="aProgress">The new progress percentage (0-100).</param>
    /// <param name="aFailureMessage">Failure description for failed/blocked states, if any.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is updated.</returns>
    Task UpdateStateAsync(Guid aJobId, string aUserId, Enums.JobState aState, int aProgress, string? aFailureMessage, CancellationToken aCancellationToken = default);
}
