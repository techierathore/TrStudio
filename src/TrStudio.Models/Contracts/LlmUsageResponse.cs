namespace TrStudio.Models.Contracts;

/// <summary>
/// Per-user text-LLM token usage rollup (RAG-003/BRD-56). Surfaced on generation responses and by
/// the <c>/api/v1/rag/usage</c> endpoint; positive-cost usage is also accrued to the LlmText spend
/// channel via the spend ledger.
/// </summary>
public class LlmUsageResponse
{
    /// <summary>Total prompt/input tokens accrued.</summary>
    public int InputTokens { get; set; }

    /// <summary>Total completion/output tokens accrued.</summary>
    public int OutputTokens { get; set; }

    /// <summary>Sum of input and output tokens.</summary>
    public int TotalTokens => InputTokens + OutputTokens;

    /// <summary>Estimated cost in USD attributed to the accrued tokens.</summary>
    public decimal EstimatedCostUsd { get; set; }

    /// <summary>Number of LLM operations accrued into this rollup.</summary>
    public int OperationCount { get; set; }
}
