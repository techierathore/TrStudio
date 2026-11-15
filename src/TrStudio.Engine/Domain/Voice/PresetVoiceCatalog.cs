using TrStudio.Models.Entities;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Voice;

/// <summary>
/// In-memory <see cref="IPresetVoiceCatalog"/> (FN-031/BRD-22): a small fully-synthetic preset-voice
/// library spanning EN / HI / Hinglish. Each voice carries its emotion/style/speed capability flags
/// (FN-034) so the UI can enable exactly the controls a voice honours. Voices added later with
/// <see cref="Add"/> (an installed voice pack) are listed ahead of the built-in library.
/// </summary>
public class PresetVoiceCatalog : IPresetVoiceCatalog
{
    private readonly object objLock = new();
    private readonly List<PresetVoice> objAdded = new();
    private IReadOnlyList<PresetVoice> objVoices = BuiltIn;

    private static readonly IReadOnlyList<PresetVoice> BuiltIn = new List<PresetVoice>
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

    private IReadOnlyList<PresetVoice> Voices => Volatile.Read(ref objVoices);

    /// <summary>
    /// Adds a preset voice (an installed voice pack) ahead of the built-in library, in the order added.
    /// A voice whose id is already listed is left as it is, so adding twice changes nothing.
    /// </summary>
    /// <param name="aVoice">The voice to add.</param>
    /// <returns>True when the voice was added; false when its id was already listed.</returns>
    public bool Add(PresetVoice aVoice)
    {
        ArgumentNullException.ThrowIfNull(aVoice);
        lock (objLock)
        {
            if (Get(aVoice.VoiceId) is not null)
            {
                return false;
            }

            objAdded.Add(aVoice);
            Volatile.Write(ref objVoices, objAdded.Concat(BuiltIn).ToList());
            return true;
        }
    }

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
