namespace TrStudio.Models.Entities;

/// <summary>
/// A persisted Prompt Studio chat session (FN-073 / BRD-91). Each session groups an ordered list of
/// <see cref="PromptChatMessage"/> turns and is owner-scoped by <see cref="UserId"/> so a creator can
/// only ever see or mutate their own sessions (per-user isolation, BRD-81).
/// </summary>
public class PromptChatSession
{
    /// <summary>Unique session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>The AppManager user id owning this session (per-user isolation, BRD-81).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Display title of the session (defaults to a neutral placeholder until renamed).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The registry id of the picked <c>text-chat</c> model for this session.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the session was created.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>UTC timestamp when the session (or its messages) was last updated.</summary>
    public DateTime UpdatedUtc { get; set; }
}
