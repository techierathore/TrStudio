namespace TrStudio.Models.Contracts;

/// <summary>
/// One prior turn of a Prompt Studio conversation, passed as history to the text LLM
/// (RAG-005 / BRD-88). Deliberately transport-neutral: no RAG/vector context, just role + text.
/// </summary>
public class PromptChatTurn
{
    /// <summary>The speaker of this turn: <c>"user"</c> or <c>"assistant"</c>.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>The plain-text content of this turn.</summary>
    public string Content { get; set; } = string.Empty;
}
