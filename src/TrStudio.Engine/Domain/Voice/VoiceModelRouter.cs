using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Voice;

/// <summary>
/// Default <see cref="IVoiceModelRouter"/> (FN-033/BRD-24). Maps a language to its preferred voice
/// model and ordered fallbacks — Hindi → IndicF5 then XTTS-Hindi, English → XTTS then OmniVoice,
/// Hinglish → IndicF5 (code-mixed) — all hidden behind the single "voice" control. The chosen
/// model's <c>Installed</c> flag reflects <see cref="VoiceOptions.InstalledModels"/>.
/// </summary>
public class VoiceModelRouter : IVoiceModelRouter
{
    private static readonly IReadOnlyDictionary<string, string[]> LanguageModelMap =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["hi"] = new[] { "indic-f5", "xtts-hindi" },
            ["hinglish"] = new[] { "indic-f5", "xtts-hindi", "xtts-v2" },
            ["en"] = new[] { "xtts-v2", "omni-voice" }
        };

    private static readonly string[] DefaultModels = { "xtts-v2", "omni-voice" };

    private readonly VoiceOptions objOptions;

    /// <summary>
    /// Initialises the router.
    /// </summary>
    /// <param name="aOptions">Voice options carrying the installed-model set.</param>
    public VoiceModelRouter(VoiceOptions aOptions)
    {
        objOptions = aOptions;
    }

    /// <inheritdoc />
    public VoiceRoute Route(string aLanguage)
    {
        var vLanguage = NormaliseLanguage(aLanguage);
        var vModels = LanguageModelMap.TryGetValue(vLanguage, out var vMapped) ? vMapped : DefaultModels;
        var vPrimary = vModels[0];
        var vInstalled = objOptions.InstalledModels.Contains(vPrimary);
        var vAlternatives = vModels.Skip(1).ToArray();
        return new VoiceRoute(vLanguage, vPrimary, vInstalled, vAlternatives);
    }

    private static string NormaliseLanguage(string aLanguage)
    {
        if (string.IsNullOrWhiteSpace(aLanguage))
        {
            return "en";
        }

        var vLower = aLanguage.Trim().ToLowerInvariant();
        return vLower switch
        {
            "en" or "en-us" or "en-in" or "english" => "en",
            "hi" or "hi-in" or "hindi" => "hi",
            "hinglish" or "hi-en" or "en-hi" => "hinglish",
            _ => vLower
        };
    }
}
