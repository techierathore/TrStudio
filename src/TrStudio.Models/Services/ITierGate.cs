using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;

namespace TrStudio.Models.Services;

/// <summary>
/// Server-side execution-tier gate (BRD-59, Architecture §7). In personal mode all tiers are
/// available; in multi-user mode paid tiers are gated via AppManager FeatureSvc and a refusal
/// carries an upgrade prompt. The job-submit path consults this gate before queueing a job.
/// </summary>
public interface ITierGate
{
    /// <summary>
    /// Checks whether a user may run a job on the requested execution tier.
    /// </summary>
    /// <param name="aUserId">The AppManager user id of the requesting user.</param>
    /// <param name="aTier">The execution tier the user selected.</param>
    /// <param name="aAccessToken">The caller's AppManager access token when available (request path); null for background/worker checks.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The gate verdict, including an upgrade prompt when refused.</returns>
    Task<TierGateResult> CheckTierAsync(string aUserId, GenerationTier aTier, string? aAccessToken = null, CancellationToken aCancellationToken = default);
}
