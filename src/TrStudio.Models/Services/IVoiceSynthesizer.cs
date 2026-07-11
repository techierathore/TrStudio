using TrStudio.Models.Contracts;

namespace TrStudio.Models.Services;

/// <summary>
/// Text-to-speech synthesis (FN-031/033/034/BRD-22..25). Selects a preset voice, routes to the
/// per-language model, applies supported emotion/style/speed controls, and persists the audio asset.
/// When no real model is installed it degrades gracefully with an actionable message and may still
/// produce a deterministic synthetic asset for tests.
/// </summary>
public interface IVoiceSynthesizer
{
    /// <summary>
    /// Synthesizes speech for a request, owner-scoped.
    /// </summary>
    /// <param name="aRequest">The synthesis request.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The synthesis result (degrade state, resolved model, applied controls, timing).</returns>
    Task<VoiceSynthesizeResponse> SynthesizeAsync(VoiceSynthesizeRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);
}
