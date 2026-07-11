using TrStudio.Models.Contracts;

namespace TrStudio.Engine.Domain.Account;

/// <summary>
/// Builds the signed-in creator's account summary (REQ-FN-068/BRD-52): their own month-to-date
/// spend aggregated by channel plus their AppManager licence status. Spend aggregation always
/// works; the licence read degrades gracefully when AppManager is unreachable.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Produces the account summary for a user. The <paramref name="aAccessToken"/> (the caller's
    /// AppManager bearer, when present) is used to read the licence; a missing or unusable token,
    /// or an unreachable AppManager, degrades the licence block to "unavailable" without affecting
    /// the spend aggregation.
    /// </summary>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aAccessToken">The caller's AppManager access token, or null when unavailable.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The account summary.</returns>
    Task<AccountSummaryResponse> GetSummaryAsync(string aUserId, string? aAccessToken, CancellationToken aCancellationToken = default);
}
