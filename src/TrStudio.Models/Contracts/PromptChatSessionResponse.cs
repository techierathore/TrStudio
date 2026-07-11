namespace TrStudio.Models.Contracts;

/// <summary>
/// Summary view of a Prompt Studio chat session returned by the FN-073 endpoints.
/// </summary>
public class PromptChatSessionResponse
{
    /// <summary>Unique session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Display title of the session.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The registry id of the picked <c>text-chat</c> model for this session.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the session (or its messages) was last updated.</summary>
    public DateTime UpdatedUtc { get; set; }
}
