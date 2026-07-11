using TrStudio.Engine.Domain.Voice;

namespace TrStudio.Tests.Unit.Voice;

/// <summary>
/// Unit tests for <see cref="VoiceModelRouter"/> (FN-033): per-language routing behind one "voice"
/// control (Hindi → IndicF5, English → XTTS) and the installed-model reporting.
/// </summary>
public class VoiceModelRouterTests
{
    private static VoiceModelRouter CreateRouter(params string[] aInstalled)
        => new(new VoiceOptions { InstalledModels = new HashSet<string>(aInstalled, StringComparer.OrdinalIgnoreCase) });

    /// <summary>Hindi routes to IndicF5 with XTTS-Hindi as a fallback.</summary>
    [Fact]
    public void HindiRoutesToIndicF5()
    {
        var vRoute = CreateRouter("indic-f5").Route("hi");

        Assert.Equal("hi", vRoute.Language);
        Assert.Equal("indic-f5", vRoute.Model);
        Assert.True(vRoute.Installed);
        Assert.Contains("xtts-hindi", vRoute.Alternatives);
    }

    /// <summary>English routes to XTTS; not-installed is reported when the model is absent.</summary>
    [Fact]
    public void EnglishRoutesToXttsAndReportsNotInstalled()
    {
        var vRoute = CreateRouter().Route("en");

        Assert.Equal("xtts-v2", vRoute.Model);
        Assert.False(vRoute.Installed);
        Assert.Contains("omni-voice", vRoute.Alternatives);
    }

    /// <summary>Hinglish (code-mixed) routes to IndicF5.</summary>
    [Fact]
    public void HinglishRoutesToIndicF5()
    {
        var vRoute = CreateRouter().Route("hinglish");
        Assert.Equal("indic-f5", vRoute.Model);
    }

    /// <summary>An unknown/blank language defaults to the English route.</summary>
    [Fact]
    public void UnknownLanguageDefaultsToEnglish()
    {
        var vRoute = CreateRouter().Route("");
        Assert.Equal("en", vRoute.Language);
        Assert.Equal("xtts-v2", vRoute.Model);
    }
}
