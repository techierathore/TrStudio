using TrStudio.Models.Entities;
using TrStudio.Models.Enums;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for the spend ledger (REQ-FN-029/NFR-007). The cap is instance-global, so the
/// month-to-date aggregates are NOT owner-filtered; individual records still carry the owning
/// user id for the audit trail.
/// </summary>
public interface ISpendRepository
{
    /// <summary>
    /// Appends a spend record for a completed billed job.
    /// </summary>
    /// <param name="aRecord">The spend record to persist.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertAsync(SpendRecord aRecord, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Returns the total spend since the given UTC instant across all billed channels.
    /// </summary>
    /// <param name="aSinceUtc">The inclusive lower bound (e.g. start of the current month).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The summed spend; zero when no records exist in range.</returns>
    Task<decimal> GetTotalSinceAsync(DateTime aSinceUtc, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Returns the spend since the given UTC instant grouped by billing channel.
    /// </summary>
    /// <param name="aSinceUtc">The inclusive lower bound (e.g. start of the current month).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A per-channel spend map; channels with no spend may be absent.</returns>
    Task<IReadOnlyDictionary<SpendChannel, decimal>> GetTotalsByChannelSinceAsync(DateTime aSinceUtc, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Returns a single user's spend since the given UTC instant grouped by billing channel
    /// (REQ-FN-068 account summary). Owner-filtered on <c>"UserId"</c> so it never leaks another
    /// user's spend.
    /// </summary>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aSinceUtc">The inclusive lower bound (e.g. start of the current month).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A per-channel spend map for the user; channels with no spend may be absent.</returns>
    Task<IReadOnlyDictionary<SpendChannel, decimal>> GetTotalsByChannelForUserSinceAsync(string aUserId, DateTime aSinceUtc, CancellationToken aCancellationToken = default);
}
