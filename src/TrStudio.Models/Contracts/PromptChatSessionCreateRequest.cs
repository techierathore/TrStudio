namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for creating a Prompt Studio chat session (<c>POST /api/v1/prompt-chat/sessions</c>, FN-073).
/// </summary>
public class PromptChatSessionCreateRequest
{
    /// <summary>Optional display title; a neutral default is used when null or blank.</summary>
    public string? Title { get; set; }

    /// <summary>The registry id of the picked <c>text-chat</c> model for this session.</summary>
    public string ModelId { get; set; } = string.Empty;
}
