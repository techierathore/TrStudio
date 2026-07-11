using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for recorded public-figure rights records (SAFE-05 / BRD-64). Owner-scoped by the
/// AppManager user id (BRD-81). Absence of a matching record blocks a public-figure depiction by
/// default (fail closed).
/// </summary>
public interface IPublicFigureRightsRepository
{
    /// <summary>
    /// Persists a rights record.
    /// </summary>
    /// <param name="aRights">The rights record to persist.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertAsync(PublicFigureRights aRights, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Determines whether a rights record exists for a public figure owned by the user.
    /// </summary>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aSubjectReference">The public-figure reference to check.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> when a rights record exists; otherwise <c>false</c> (block by default).</returns>
    Task<bool> HasRightsAsync(string aUserId, string aSubjectReference, CancellationToken aCancellationToken = default);
}
