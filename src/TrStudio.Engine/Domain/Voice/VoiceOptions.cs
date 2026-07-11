using Microsoft.Extensions.Configuration;

namespace TrStudio.Engine.Domain.Voice;

/// <summary>
/// Configuration for the voice vertical (section <c>Voice</c>, FN-031..035). Real voice models are
/// not installed by default, so synthesis degrades gracefully; <see cref="SyntheticFallback"/>
/// (dev-default true) additionally emits a deterministic silent-audio asset so the pipeline and its
/// timing metadata can be exercised end-to-end without a model.
/// </summary>
public class VoiceOptions
{
    /// <summary>Whether a real TTS model backend is wired and enabled (default false — none installed).</summary>
    public bool SynthesisEnabled { get; set; }

    /// <summary>Whether to emit a deterministic synthetic audio asset when no real model is installed (default true).</summary>
    public bool SyntheticFallback { get; set; } = true;

    /// <summary>The set of voice model ids reported as installed (drives router "installed" flags). Empty by default.</summary>
    public HashSet<string> InstalledModels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The actionable message surfaced when a voice model is required but not installed.</summary>
    public const string ModelNotInstalledMessage =
        "Voice model not installed — install a TTS model (IndicF5/XTTS for Hindi, XTTS/OmniVoice for English) " +
        "under Admin > Models, or run in dev with Voice:SyntheticFallback=true to preview with placeholder audio.";

    /// <summary>
    /// Builds options from configuration, reading the <c>Voice</c> section key-by-key so the engine
    /// needs no configuration-binder package; missing keys keep their defaults.
    /// </summary>
    /// <param name="aConfiguration">The application configuration.</param>
    /// <returns>The populated options instance.</returns>
    public static VoiceOptions FromConfiguration(IConfiguration aConfiguration)
    {
        var vOptions = new VoiceOptions();
        if (bool.TryParse(aConfiguration["Voice:SynthesisEnabled"], out var vEnabled)) { vOptions.SynthesisEnabled = vEnabled; }
        if (bool.TryParse(aConfiguration["Voice:SyntheticFallback"], out var vFallback)) { vOptions.SyntheticFallback = vFallback; }

        var vInstalled = aConfiguration.GetSection("Voice:InstalledModels").GetChildren();
        foreach (var vChild in vInstalled)
        {
            if (!string.IsNullOrWhiteSpace(vChild.Value))
            {
                vOptions.InstalledModels.Add(vChild.Value);
            }
        }

        return vOptions;
    }
}
