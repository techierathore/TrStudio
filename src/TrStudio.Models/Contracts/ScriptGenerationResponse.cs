namespace TrStudio.Models.Contracts;

/// <summary>
/// Result of a script generation request (RAG-001/BRD-53). When no text LLM is configured the
/// response degrades gracefully — <see cref="Degraded"/> is set and <see cref="Message"/> carries
/// an actionable hint — while a deterministic dev expansion may still be returned.
/// </summary>
public class ScriptGenerationResponse
{
    /// <summary>The generated (or synthetic-dev) script text; null when unavailable.</summary>
    public string? Script { get; set; }

    /// <summary>The requested language echoed back.</summary>
    public string Language { get; set; } = "en";

    /// <summary>Whether the result came from a degraded/synthetic path rather than a live LLM.</summary>
    public bool Degraded { get; set; }

    /// <summary>The path that produced this result: <c>techierag-llm</c>, <c>synthetic-dev</c> or <c>unavailable</c>.</summary>
    public string Mode { get; set; } = "unavailable";

    /// <summary>An actionable message when degraded (e.g. how to configure the LLM); otherwise null.</summary>
    public string? Message { get; set; }

    /// <summary>Token usage accrued by this call (feeds the LlmText spend channel, RAG-003).</summary>
    public LlmUsageResponse Usage { get; set; } = new();
}
