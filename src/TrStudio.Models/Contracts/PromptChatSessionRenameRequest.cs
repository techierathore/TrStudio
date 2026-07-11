namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for renaming a Prompt Studio chat session (<c>PUT /api/v1/prompt-chat/sessions/{id}</c>, FN-073).
/// </summary>
public class PromptChatSessionRenameRequest
{
    /// <summary>The new display title.</summary>
    public string Title { get; set; } = string.Empty;
}
