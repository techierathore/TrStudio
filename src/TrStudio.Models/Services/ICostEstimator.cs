using TrStudio.Models.Entities;

namespace TrStudio.Models.Services;

/// <summary>
/// Estimates the monetary cost of a generation job before it is enqueued — part of the
/// cost guardrails and monthly spend cap (Architecture §5, NFR-07, ADM-10).
/// </summary>
public interface ICostEstimator
{
    /// <summary>
    /// Estimates the cost of running a job on its resolved model/tier.
    /// </summary>
    /// <param name="aJob">The job to estimate.</param>
    /// <param name="aCancellationToken">Token to cancel the estimation.</param>
    /// <returns>The estimated cost in the account currency; zero for free local execution.</returns>
    Task<decimal> EstimateAsync(GenerationJob aJob, CancellationToken aCancellationToken = default);
}
