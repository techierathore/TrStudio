using TrStudio.Models.Entities;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Voice;

/// <summary>
/// In-memory <see cref="IPresetVoiceCatalog"/> (FN-031/BRD-22): a small fully-synthetic preset-voice
/// library spanning EN / HI / Hinglish. Each voice carries its emotion/style/speed capability flags
/// (FN-034) so the UI can enable exactly the controls a voice honours.
/// </summary>
public class PresetVoiceCatalog : IPresetVoiceCatalog
{
    private static readonly IReadOnlyList<PresetVoice> Voices = new List<PresetVoice>
    {
        new()
        {
            VoiceId = "en-aria", DisplayName = "Aria (English, warm)", Language = "en", Gender = "female",
            SupportsEmotion = true, SupportsStyle = true, SupportsSpeed = true,
            Emotions = new[] { "neutral", "cheerful", "serious", "excited" }
        },
        new()
        {
            VoiceId = "en-ravi", DisplayName = "Ravi (English, narrator)", Language = "en", Gender = "male",
            SupportsEmotion = false, SupportsStyle = true, SupportsSpeed = true,
            Emotions = Array.Empty<string>()
        },
        new()
        {
            VoiceId = "hi-meera", DisplayName = "Meera (Hindi, expressive)", Language = "hi", Gender = "female",
            SupportsEmotion = true, SupportsStyle = false, SupportsSpeed = true,
            Emotions = new[] { "neutral", "khush", "gambhir" }
        },
        new()
        {
            VoiceId = "hi-arjun", DisplayName = "Arjun (Hindi, anchor)", Language = "hi", Gender = "male",
            SupportsEmotion = false, SupportsStyle = false, SupportsSpeed = true,
            Emotions = Array.Empty<string>()
        },
        new()
        {
            VoiceId = "hinglish-zoya", DisplayName = "Zoya (Hinglish, casual)", Language = "hinglish", Gender = "female",
            SupportsEmotion = true, SupportsStyle = true, SupportsSpeed = true,
            Emotions = new[] { "neutral", "cheerful", "chill" }
        }
    };

    /// <inheritdoc />
    public IReadOnlyList<PresetVoice> GetAll() => Voices;

    /// <inheritdoc />
    public IReadOnlyList<PresetVoice> GetByLanguage(string aLanguage)
    {
        if (string.IsNullOrWhiteSpace(aLanguage))
        {
            return Voices;
        }

        var vLanguage = aLanguage.Trim();
        return Voices.Where(aVoice => string.Equals(aVoice.Language, vLanguage, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <inheritdoc />
    public PresetVoice? Get(string aVoiceId)
        => Voices.FirstOrDefault(aVoice => string.Equals(aVoice.VoiceId, aVoiceId, StringComparison.OrdinalIgnoreCase));
}
