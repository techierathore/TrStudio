using TrStudio.Tests.Unit.Venice;

namespace TrStudio.Tests.Unit.Desktop;

/// <summary>
/// Found on the real Mac app, 15 Nov 2026: at the default 1.6 zoom the Login card sat far right of
/// centre, because WKWebView.PageZoom on Mac Catalyst magnifies the page without laying it out again.
/// The Mac head must zoom with CSS zoom and publish --tr-zoom, which .auth-shell divides by.
/// The app project is a MAUI head the unit tests cannot load, so the rule is read from its source.
/// </summary>
public class MacZoomRuleTests
{
    [Fact(DisplayName = "REQ-FN-104 Mac zoom uses CSS zoom and --tr-zoom, never WKWebView.PageZoom at the level")]
    public void MacZoomUsesCssZoom()
    {
        var vSource = File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "TrStudio", "BlazorZoom.cs"));
        var vMac = vSource[vSource.IndexOf("#if MACCATALYST", vSource.IndexOf("namespace", StringComparison.Ordinal), StringComparison.Ordinal)..vSource.IndexOf("#elif WINDOWS", StringComparison.Ordinal)];

        Assert.DoesNotContain("PageZoom = (nfloat)Level", vMac);
        Assert.Contains("style.zoom", vMac);
        Assert.Contains("--tr-zoom", vMac);
        Assert.Contains("WKUserScriptInjectionTime.AtDocumentStart", vMac);
    }
}
