namespace TrStudio.Models.Contracts;

/// <summary>
/// Result of a character-bible retrieval (RAG-004): the persona-notes entries most relevant to a
/// script-assist query, plus the retrieval mode that produced them. When no embedding model is
/// available the vector path degrades to a deterministic keyword match over the persisted entries.
/// </summary>
public class CharacterBibleRetrievalResponse
{
    /// <summary>The owning character.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>The script-assist query the entries were retrieved for.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>The retrieved entries, most-relevant first.</summary>
    public List<CharacterBibleEntryResponse> Entries { get; set; } = new();

    /// <summary>The retrieval path: <c>techierag-vector</c> or <c>keyword-fallback</c>.</summary>
    public string Mode { get; set; } = "keyword-fallback";

    /// <summary>Whether retrieval fell back off the vector path (no embedding model available).</summary>
    public bool Degraded { get; set; }

    /// <summary>An actionable message when degraded; otherwise null.</summary>
    public string? Message { get; set; }
}
