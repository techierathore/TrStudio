namespace TrStudio.Models.Contracts;

/// <summary>
/// Result of posting a user message: the persisted user turn plus the assistant reply (when the
/// request was not rejected), and the graceful-degrade / rejection signals (FN-073 / BRD-90/91).
/// </summary>
public class PromptChatPostMessageResponse
{
    /// <summary>The persisted user message turn.</summary>
    public PromptChatMessageResponse UserMessage { get; set; } = new();

    /// <summary>The persisted assistant reply turn, or null when the request was rejected.</summary>
    public PromptChatMessageResponse? AssistantMessage { get; set; }

    /// <summary>True when the completion was refused before any reply was persisted (see <see cref="RejectionReason"/>).</summary>
    public bool Rejected { get; set; }

    /// <summary>Clear, actionable reason when <see cref="Rejected"/> is true; otherwise null.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>True when the reply came from the degrade path (LLM not configured / unreachable).</summary>
    public bool Degraded { get; set; }
}
