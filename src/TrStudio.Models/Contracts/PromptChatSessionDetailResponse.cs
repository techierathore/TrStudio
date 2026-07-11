namespace TrStudio.Models.Contracts;

/// <summary>
/// Full view of a Prompt Studio chat session plus its ordered messages
/// (<c>GET /api/v1/prompt-chat/sessions/{id}</c>, FN-073).
/// </summary>
public class PromptChatSessionDetailResponse
{
    /// <summary>The session summary.</summary>
    public PromptChatSessionResponse Session { get; set; } = new();

    /// <summary>The session's messages, oldest first.</summary>
    public List<PromptChatMessageResponse> Messages { get; set; } = new();
}
