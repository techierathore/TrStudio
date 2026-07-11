namespace TrStudio.Models.Entities;

/// <summary>
/// One persisted turn of a Prompt Studio chat session (FN-073 / BRD-91). Owner-scoped by
/// <see cref="UserId"/> (mirrors its parent <see cref="PromptChatSession"/>) so every read/write
/// applies the owner filter server-side (per-user isolation, BRD-81).
/// </summary>
public class PromptChatMessage
{
    /// <summary>Unique message identifier.</summary>
    public Guid MessageId { get; set; }

    /// <summary>The parent session this message belongs to.</summary>
    public Guid SessionId { get; set; }

    /// <summary>The AppManager user id owning this message (per-user isolation, BRD-81).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The speaker of this turn: <c>"user"</c> or <c>"assistant"</c>.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>The plain-text content of this turn.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>True when the originating user message carried an attached image.</summary>
    public bool HasImage { get; set; }

    /// <summary>UTC timestamp when the message was created.</summary>
    public DateTime CreatedUtc { get; set; }
}
