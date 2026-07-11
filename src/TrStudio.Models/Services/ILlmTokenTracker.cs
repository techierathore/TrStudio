using TrStudio.Models.Contracts;

namespace TrStudio.Models.Services;

/// <summary>
/// Text-LLM token-budget tracking (RAG-003/BRD-56). Accrues token counts and cost per user for the
/// text-LLM channel and FEEDS the spend rollup: positive-cost usage is recorded to the
/// <c>LlmText</c> spend channel via the spend ledger so it appears in the monthly-cap report.
/// </summary>
public interface ILlmTokenTracker
{
    /// <summary>
    /// Accrues one text-LLM operation's usage for a user and, when billed, records it against the
    /// LlmText spend channel.
    /// </summary>
    /// <param name="aUserId">The AppManager user id the usage is attributed to.</param>
    /// <param name="aModelId">The model/provider the tokens were spent on.</param>
    /// <param name="aInputTokens">Prompt/input tokens.</param>
    /// <param name="aOutputTokens">Completion/output tokens.</param>
    /// <param name="aCostUsd">Estimated cost in USD (0 for free/local — accrues tokens but no spend).</param>
    /// <param name="aJobId">Optional correlating job id for the spend record's audit trail.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the usage is accrued (and any spend recorded).</returns>
    Task RecordUsageAsync(string aUserId, string aModelId, int aInputTokens, int aOutputTokens, decimal aCostUsd, Guid? aJobId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Returns the per-user session token-usage rollup.
    /// </summary>
    /// <param name="aUserId">The AppManager user id.</param>
    /// <returns>The accrued token usage for the user (zeroed when none).</returns>
    LlmUsageResponse GetUsage(string aUserId);
}
