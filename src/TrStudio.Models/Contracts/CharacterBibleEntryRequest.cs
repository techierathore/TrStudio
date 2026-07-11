namespace TrStudio.Models.Contracts;

/// <summary>
/// Request to add a persona-notes entry to a character's RAG-backed bible (RAG-004/BRD-55).
/// </summary>
public class CharacterBibleEntryRequest
{
    /// <summary>A short title/category for the note (e.g. "Backstory", "Voice").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The persona note content applied during script-assist (required).</summary>
    public string Content { get; set; } = string.Empty;
}
