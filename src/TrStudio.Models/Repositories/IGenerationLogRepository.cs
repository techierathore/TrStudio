using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for the append-only generation log (audit trail, SAFE-06). Reads of user-owned
/// rows take the owning AppManager user id and apply the owner filter server-side (BRD-81).
/// </summary>
public interface IGenerationLogRepository
{
    /// <summary>
    /// Appends a generation-log entry. The entry's <see cref="GenerationLogEntry.UserId"/> records ownership.
    /// </summary>
    /// <param name="aEntry">The entry to append.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertAsync(GenerationLogEntry aEntry, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Gets a single generation-log entry by id, owner-filtered (used by one-click reproduce).
    /// </summary>
    /// <param name="aLogId">The log entry identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The entry, or null when not found or not owned by <paramref name="aUserId"/>.</returns>
    Task<GenerationLogEntry?> GetAsync(Guid aLogId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists the user's generation-log entries newest-first, paginated.
    /// </summary>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aPage">The 1-based page number.</param>
    /// <param name="aPageSize">The page size.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A page of the user's log entries.</returns>
    Task<PagedResult<GenerationLogEntry>> ListAsync(string aUserId, int aPage, int aPageSize, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Returns generation-outcome counts (e.g. <c>succeeded</c>/<c>failed</c>/<c>blocked</c>) since
    /// the given UTC instant across ALL users — the admin ops dashboard's error-rate source
    /// (REQ-FN-064). This is an instance-global aggregate, NOT owner-filtered; it is only ever
    /// exposed behind the admin policy.
    /// </summary>
    /// <param name="aSinceUtc">The inclusive lower bound of the window.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A map of outcome label to count; outcomes with no rows are absent.</returns>
    Task<IReadOnlyDictionary<string, int>> GetOutcomeCountsSinceAsync(DateTime aSinceUtc, CancellationToken aCancellationToken = default);
}
