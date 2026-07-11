namespace TrStudio.Models.Contracts;

/// <summary>
/// View of a single persisted Prompt Studio chat message returned by the FN-073 endpoints.
/// </summary>
public class PromptChatMessageResponse
{
    /// <summary>Unique message identifier.</summary>
    public Guid MessageId { get; set; }

    /// <summary>The speaker of this turn: <c>"user"</c> or <c>"assistant"</c>.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>The plain-text content of this turn.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>True when the originating user message carried an attached image.</summary>
    public bool HasImage { get; set; }

    /// <summary>UTC timestamp when the message was created.</summary>
    public DateTime CreatedUtc { get; set; }
}
