namespace TrStudio.Models.Contracts;

/// <summary>
/// Request to produce caption/subtitle text from a transcript (RAG-002/BRD-54). The transcript is
/// produced by Whisper upstream (consumed later by FN-040); here the text is cleaned/segmented via
/// TechieRag with a deterministic fallback.
/// </summary>
public class CaptionGenerationRequest
{
    /// <summary>The source transcript text (from Whisper) to turn into captions (required).</summary>
    public string Transcript { get; set; } = string.Empty;

    /// <summary>Target language: <c>en</c>, <c>hi</c> or <c>hinglish</c> (default <c>en</c>).</summary>
    public string Language { get; set; } = "en";

    /// <summary>Output format: <c>srt</c> (timed) or <c>plain</c> (default <c>srt</c>).</summary>
    public string Format { get; set; } = "srt";

    /// <summary>Optional producing voice/media job id (association only).</summary>
    public Guid? JobId { get; set; }
}
