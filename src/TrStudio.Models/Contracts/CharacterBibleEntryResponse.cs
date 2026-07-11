namespace TrStudio.Models.Contracts;

/// <summary>
/// The creator-facing view of a character-bible entry (RAG-004).
/// </summary>
public class CharacterBibleEntryResponse
{
    /// <summary>Unique bible-entry identifier.</summary>
    public Guid EntryId { get; set; }

    /// <summary>The owning character.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>The note title/category.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The persona note content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the entry was created.</summary>
    public DateTime CreatedUtc { get; set; }
}
