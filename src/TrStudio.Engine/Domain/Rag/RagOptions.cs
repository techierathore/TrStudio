using Microsoft.Extensions.Configuration;

namespace TrStudio.Engine.Domain.Rag;

/// <summary>
/// Configuration for the text-RAG features (section <c>Rag</c>, RAG-001/002/003). No text LLM is
/// configured by default, so generation degrades gracefully; <see cref="SyntheticFallback"/>
/// (dev-default true) additionally returns a deterministic dev expansion and accrues an estimated
/// token spend against the <c>LlmText</c> channel so the token-budget path (RAG-003) is exercisable
/// without a live model.
/// </summary>
public class RagOptions
{
    /// <summary>Whether to return a deterministic dev expansion when no LLM is configured (default true).</summary>
    public bool SyntheticFallback { get; set; } = true;

    /// <summary>USD cost per 1,000 tokens used to price the synthetic-dev path's estimated usage (default $0.002).</summary>
    public decimal SyntheticCostPerKTokenUsd { get; set; } = 0.002m;

    /// <summary>The actionable message surfaced when a text LLM is required but not configured.</summary>
    public const string LlmNotConfiguredMessage =
        "Text LLM not configured — set TechieRag:Llm:Source (e.g. Ollama, OpenAICompatible, Anthropic) with an " +
        "endpoint/model under appsettings to enable real script/caption generation.";

    /// <summary>
    /// Builds options from configuration, reading the <c>Rag</c> section key-by-key so the engine
    /// needs no configuration-binder package; missing keys keep their defaults.
    /// </summary>
    /// <param name="aConfiguration">The application configuration.</param>
    /// <returns>The populated options instance.</returns>
    public static RagOptions FromConfiguration(IConfiguration aConfiguration)
    {
        var vOptions = new RagOptions();
        if (bool.TryParse(aConfiguration["Rag:SyntheticFallback"], out var vFallback)) { vOptions.SyntheticFallback = vFallback; }
        if (decimal.TryParse(aConfiguration["Rag:SyntheticCostPerKTokenUsd"], out var vRate)) { vOptions.SyntheticCostPerKTokenUsd = vRate; }
        return vOptions;
    }
}
