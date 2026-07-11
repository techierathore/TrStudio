using TrStudio.Models.Contracts;

namespace TrStudio.Models.Services;

/// <summary>
/// The non-disableable safety pipeline (SAFE-01..06 / BRD-60..65). Runs the floors on EVERY
/// provider: a pre-check on the submitted inputs before the job is enqueued, and a post-check on
/// the produced output before the asset is released. Every evaluation — allow or block — is written
/// to the audit trail (SAFE-06). The floors are enforced regardless of any mode, including the
/// owner-only Unrestricted (NSFW) mode. Uncertain checks fail closed.
/// </summary>
public interface ISafetyPipeline
{
    /// <summary>
    /// Evaluates the floors against a job's inputs BEFORE it is enqueued.
    /// </summary>
    /// <param name="aContext">The generation context to evaluate.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>An allow or block decision; the caller must not enqueue a blocked job.</returns>
    Task<SafetyDecision> PreCheckAsync(SafetyEvaluationContext aContext, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Evaluates the floors against a produced output BEFORE the asset is released.
    /// </summary>
    /// <param name="aContext">The generation context to evaluate.</param>
    /// <param name="aOutput">The produced output sample (binary + media kind).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>An allow or block decision; the caller must quarantine a blocked output.</returns>
    Task<SafetyDecision> PostCheckAsync(SafetyEvaluationContext aContext, SafetyContentSample aOutput, CancellationToken aCancellationToken = default);
}
