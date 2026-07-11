using TrStudio.Models.Contracts;

namespace TrStudio.Models.Services;

/// <summary>
/// Text generation over TechieRag (RAG-001/002/BRD-53/54): script generation/expansion from a brief
/// and caption/subtitle text from a Whisper transcript. Degrades gracefully when no LLM is configured.
/// </summary>
public interface ITextGenerationService
{
    /// <summary>
    /// Generates or expands a script from a brief (RAG-001).
    /// </summary>
    /// <param name="aRequest">The brief and generation options.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81; token attribution).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The generated script or a graceful degrade.</returns>
    Task<ScriptGenerationResponse> GenerateScriptAsync(ScriptGenerationRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Produces caption/subtitle text from a transcript (RAG-002).
    /// </summary>
    /// <param name="aRequest">The transcript and caption options.</param>
    /// <param name="aUserId">The owning AppManager user id (token attribution).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The captions or a graceful degrade.</returns>
    Task<CaptionGenerationResponse> GenerateCaptionsAsync(CaptionGenerationRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);
}
