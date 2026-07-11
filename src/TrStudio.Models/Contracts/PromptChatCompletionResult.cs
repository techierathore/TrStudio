namespace TrStudio.Models.Contracts;

/// <summary>
/// Result of a Prompt Studio chat round-trip (RAG-005 / BRD-88). Carries the crafted-prompt reply,
/// token accounting (fed to the BRD-56 tracker / <c>SpendChannel.LlmText</c>), and the graceful-degrade
/// signal used when no LLM is configured or the endpoint is unreachable (mirrors RAG-001).
/// </summary>
public class PromptChatCompletionResult
{
    /// <summary>The assistant reply text (the crafted prompt), or a degrade message.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>True when the reply came from the degrade path (LLM not configured / unreachable), not a live model.</summary>
    public bool Degraded { get; set; }

    /// <summary>Human-readable mode tag: <c>"live"</c>, <c>"synthetic-dev"</c>, or <c>"unavailable"</c>.</summary>
    public string Mode { get; set; } = "live";

    /// <summary>
    /// True when the request was refused before any LLM call — e.g. an image attached to a
    /// non-vision model (BRD-90), or the model id is not a usable text-chat row. See <see cref="RejectionReason"/>.
    /// </summary>
    public bool Rejected { get; set; }

    /// <summary>Clear, actionable reason when <see cref="Rejected"/> is true; otherwise null.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Prompt (input) tokens consumed by this round-trip.</summary>
    public int InputTokens { get; set; }

    /// <summary>Completion (output) tokens produced by this round-trip.</summary>
    public int OutputTokens { get; set; }

    /// <summary>Estimated USD cost accrued to the caller's text-LLM budget for this round-trip.</summary>
    public decimal EstimatedCostUsd { get; set; }
}
