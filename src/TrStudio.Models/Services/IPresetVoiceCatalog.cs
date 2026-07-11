using TrStudio.Models.Entities;

namespace TrStudio.Models.Services;

/// <summary>
/// The built-in preset-voice library (FN-031/BRD-22). Preset voices are fully-synthetic and cover
/// EN / HI / Hinglish.
/// </summary>
public interface IPresetVoiceCatalog
{
    /// <summary>Returns every preset voice.</summary>
    IReadOnlyList<PresetVoice> GetAll();

    /// <summary>Returns the preset voices for a language (<c>en</c>, <c>hi</c>, <c>hinglish</c>).</summary>
    /// <param name="aLanguage">The language filter.</param>
    IReadOnlyList<PresetVoice> GetByLanguage(string aLanguage);

    /// <summary>Returns a preset voice by id, or null when unknown.</summary>
    /// <param name="aVoiceId">The preset voice id.</param>
    PresetVoice? Get(string aVoiceId);
}
