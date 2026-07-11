namespace TrStudio.Models.Services;

/// <summary>
/// Per-language voice-model routing hidden behind the single "voice" control (FN-033/BRD-24):
/// Hindi routes to IndicF5 / XTTS-Hindi, English to XTTS / OmniVoice, and so on. The UI never sees
/// the model — only the resolved route is reported.
/// </summary>
public interface IVoiceModelRouter
{
    /// <summary>
    /// Resolves the voice model for a language.
    /// </summary>
    /// <param name="aLanguage">The language: <c>en</c>, <c>hi</c> or <c>hinglish</c>.</param>
    /// <returns>The resolved route (model id, whether installed, and any fallback alternatives).</returns>
    VoiceRoute Route(string aLanguage);
}

/// <summary>
/// The outcome of per-language voice-model routing (FN-033).
/// </summary>
/// <param name="Language">The normalised language the route was resolved for.</param>
/// <param name="Model">The chosen model id (e.g. <c>indic-f5</c>, <c>xtts-v2</c>).</param>
/// <param name="Installed">Whether the chosen model is installed and runnable on this instance.</param>
/// <param name="Alternatives">Ordered fallback model ids for the language.</param>
public record VoiceRoute(string Language, string Model, bool Installed, IReadOnlyList<string> Alternatives);
