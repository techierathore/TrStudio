using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Append-only data access for the safety audit trail (SAFE-06 / BRD-65). Every generation
/// decision — allow or block — is recorded here. The admin audit surface reads the deployment-wide
/// trail; per-user reads apply the owner filter (BRD-81).
/// </summary>
public interface ISafetyAuditRepository
{
    /// <summary>
    /// Appends a safety audit entry.
    /// </summary>
    /// <param name="aEntry">The entry to append.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertAsync(SafetyAuditEntry aEntry, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists the deployment-wide audit trail newest-first, paginated (admin-scoped).
    /// </summary>
    /// <param name="aPage">The 1-based page number.</param>
    /// <param name="aPageSize">The page size.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A page of audit entries across all users.</returns>
    Task<PagedResult<SafetyAuditEntry>> ListAsync(int aPage, int aPageSize, CancellationToken aCancellationToken = default);
}
