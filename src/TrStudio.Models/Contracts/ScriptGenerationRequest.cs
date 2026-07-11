namespace TrStudio.Models.Contracts;

/// <summary>
/// Request to generate or expand a script from a creative brief (RAG-001/BRD-53). The text LLM is
/// driven through TechieRag; language covers EN/HI/Hinglish.
/// </summary>
public class ScriptGenerationRequest
{
    /// <summary>The creative brief to expand into a script (required).</summary>
    public string Brief { get; set; } = string.Empty;

    /// <summary>Target language: <c>en</c>, <c>hi</c> or <c>hinglish</c> (default <c>en</c>).</summary>
    public string Language { get; set; } = "en";

    /// <summary>Optional tone/style hint (e.g. "energetic", "cinematic").</summary>
    public string? Tone { get; set; }

    /// <summary>Optional target duration in seconds, used to size the script.</summary>
    public int? TargetDurationSeconds { get; set; }

    /// <summary>Optional owning project id (audit/association only).</summary>
    public Guid? ProjectId { get; set; }
}
