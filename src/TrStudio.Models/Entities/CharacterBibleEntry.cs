namespace TrStudio.Models.Entities;

/// <summary>
/// A single persona-notes entry in a character's RAG-backed "bible" (RAG-004/BRD-55). Entries are
/// the durable source of truth (owner + character scoped, BRD-81) and are additionally indexed into
/// the vector store via TechieRag so script-assist can retrieve and apply the persona's canon.
/// Retrieval degrades to a keyword match over these rows when no embedding model is available.
/// </summary>
public class CharacterBibleEntry
{
    /// <summary>Unique bible-entry identifier.</summary>
    public Guid EntryId { get; set; }

    /// <summary>The character this entry belongs to.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>The AppManager user id owning this entry (owner filter, BRD-81).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>A short title/category for the note (e.g. "Backstory", "Voice", "Wardrobe").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The persona note content applied during script-assist.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the entry was created.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>UTC timestamp when the entry was last updated.</summary>
    public DateTime UpdatedUtc { get; set; }
}
