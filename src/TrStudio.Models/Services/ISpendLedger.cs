using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;

namespace TrStudio.Models.Services;

/// <summary>
/// The spend-cap guardrail service (REQ-FN-029/BRD-13, NFR-007). Records spend per completed
/// billed job, reports month-to-date spend against the configured monthly cap, and evaluates a
/// pending job's estimate against the cap so the orchestrator can warn or block at submit.
/// </summary>
public interface ISpendLedger
{
    /// <summary>
    /// Records the actual cost of a completed billed job. Zero-cost (local/free) jobs are ignored.
    /// </summary>
    /// <param name="aUserId">The AppManager user id that owned the job (audit only).</param>
    /// <param name="aJobId">The completed job's identifier.</param>
    /// <param name="aModelId">The registry model the job ran against.</param>
    /// <param name="aChannel">The billing channel the spend accrued against.</param>
    /// <param name="aAmountUsd">The actual cost incurred, in the account currency.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the record is persisted (or skipped for a zero amount).</returns>
    Task RecordAsync(string aUserId, Guid aJobId, string aModelId, SpendChannel aChannel, decimal aAmountUsd, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Builds the month-to-date spend summary (spend by channel, cap and remaining headroom).
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The spend summary snapshot.</returns>
    Task<SpendSummaryResponse> GetSummaryAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Evaluates a pending job's cost estimate against the monthly cap, deciding whether the
    /// submit should be blocked, merely warned, or allowed silently.
    /// </summary>
    /// <param name="aPendingEstimate">The pending job's cost estimate.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The guard decision with an actionable message when relevant.</returns>
    Task<SpendGuardResult> EvaluateAsync(decimal aPendingEstimate, CancellationToken aCancellationToken = default);
}

/// <summary>
/// The outcome of a pre-submit spend-cap evaluation (REQ-FN-029).
/// </summary>
/// <param name="SpentToDate">Month-to-date spend before this job.</param>
/// <param name="Cap">The configured monthly cap.</param>
/// <param name="Projected">Projected month-to-date spend if this job runs.</param>
/// <param name="WouldExceed">Whether the projected spend exceeds the cap for a billed job.</param>
/// <param name="ShouldWarn">Whether the projected spend crosses the warn threshold without exceeding the cap.</param>
/// <param name="Message">An actionable message when the submit is blocked or warned; otherwise null.</param>
public record SpendGuardResult(
    decimal SpentToDate,
    decimal Cap,
    decimal Projected,
    bool WouldExceed,
    bool ShouldWarn,
    string? Message);
