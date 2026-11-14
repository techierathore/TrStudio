using TrStudio.Models.Entities;

namespace TrStudio.Engine.Domain.Jobs;

/// <summary>
/// A provider whose work continues at the provider after it was queued, so a job running when the
/// app closed can be picked up on the next start (phase 3, REQ-FN-094). Venice clips implement it:
/// the reference is Venice's queue id.
/// </summary>
public interface IResumableGenerationProvider
{
    /// <summary>The provider's own reference for a submitted job, once it has one.</summary>
    /// <param name="aJobId">The job id.</param>
    /// <returns>The reference, or null.</returns>
    string? GetProviderRef(Guid aJobId);

    /// <summary>Re-attaches to a job queued at the provider before the app closed.</summary>
    /// <param name="aJob">The stored job.</param>
    /// <param name="aProviderRef">The reference stored when it was queued.</param>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>True when polling can continue; false when the job cannot be resumed.</returns>
    Task<bool> ResumeAsync(GenerationJob aJob, string aProviderRef, CancellationToken aCancellationToken);
}
