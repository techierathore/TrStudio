namespace TrStudio.Models.Contracts;

/// <summary>
/// Result of a caption/subtitle generation request (RAG-002/BRD-54). Degrades gracefully when no
/// Whisper transcript or text LLM is available.
/// </summary>
public class CaptionGenerationResponse
{
    /// <summary>The generated captions (SRT or plain); null when unavailable.</summary>
    public string? Captions { get; set; }

    /// <summary>The output format echoed back (<c>srt</c> or <c>plain</c>).</summary>
    public string Format { get; set; } = "srt";

    /// <summary>Whether the result came from a degraded/synthetic path rather than a live LLM.</summary>
    public bool Degraded { get; set; }

    /// <summary>The path that produced this result: <c>techierag-llm</c>, <c>synthetic-dev</c> or <c>unavailable</c>.</summary>
    public string Mode { get; set; } = "unavailable";

    /// <summary>An actionable message when degraded; otherwise null.</summary>
    public string? Message { get; set; }

    /// <summary>Token usage accrued by this call (feeds the LlmText spend channel, RAG-003).</summary>
    public LlmUsageResponse Usage { get; set; } = new();
}
