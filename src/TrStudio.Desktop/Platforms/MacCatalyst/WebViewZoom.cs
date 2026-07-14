using System.Globalization;
using Foundation;
using UIKit;
using WebKit;

namespace TrStudio.Desktop.Platforms.MacCatalyst;

/// <summary>
/// Browser-style page zoom for the Mac Catalyst desktop shell (REQ-FN-072 UAT: 4K monitors made the
/// WebView text unreadably small). Everything runs inside the WebView via an injected controller
/// script, because two native paths proved unusable on this Catalyst/WebKit build:
/// <list type="bullet">
/// <item><description><c>WKWebView.PageZoom</c> is a no-op — setting it resizes nothing.</description></item>
/// <item><description>MAUI <c>MenuBarItems</c> never render in the Catalyst menu bar, so a native
/// "Zoom" menu (and its Cmd+/Cmd- accelerators) never appeared — the earlier menu-based feature was
/// invisible.</description></item>
/// </list>
/// Instead, on <c>BlazorWebViewInitialized</c> we install a <see cref="WKUserScript"/> (document-start)
/// that: (1) applies the CSS <c>zoom</c> on <c>document.documentElement</c> — which cascades to Blazor's
/// rendered content and reflows correctly; (2) defaults the first run to the DPI-corrected
/// <see cref="Baseline"/> (keyed off logical point width — see there); (3) binds <b>Cmd/Ctrl + '='</b>
/// (zoom in), <b>'-'</b> (zoom out) and <b>'0'</b> (actual size) and persists the chosen level in
/// <c>localStorage</c>, so zoom-in/out work and survive restarts without any native menu. Clamped
/// 0.5–3.0 in 0.1 steps.
/// </summary>
public static class WebViewZoom
{
    /// <summary>The smallest allowed zoom level.</summary>
    private const double MinZoom = 0.5;

    /// <summary>The largest allowed zoom level.</summary>
    private const double MaxZoom = 3.0;

    /// <summary>The logical point width that maps to a 1.0 default. Tuned so a 4K panel driven at
    /// native/scale-1.0 (~3840 points) defaults to ~1.6× — the owner's preferred baseline — while a
    /// 1080p / HiDPI display (~1920 points) stays at 1.0 (0.8, clamped up). Raise this to make the
    /// out-of-the-box zoom gentler, lower it to make it stronger.</summary>
    private const double ReferenceWidthPoints = 2400.0;

    /// <summary>
    /// The readable 100% baseline for the current display. Keys off the main screen's <b>logical
    /// point width</b> rather than its backing scale: a 4K panel driven at its native resolution
    /// (scale 1.0 — the case the owner hit) reports ~3840 points wide and makes everything physically
    /// tiny, yet its backing scale is 1.0, so a scale-based default did nothing. A HiDPI display (or
    /// 1080p) reports ~1920 points and already reads fine. Defaulting the zoom to
    /// <c>points / <see cref="ReferenceWidthPoints"/></c> therefore scales up proportionally on
    /// low-scale high-resolution panels (~1.6 at 3840pt) while leaving normal displays at 1.0.
    /// Never auto-shrinks below 1.0; clamped to <see cref="MaxZoom"/>. Falls back to 1.0 if the
    /// screen metrics are unavailable.
    /// </summary>
    private static double Baseline
    {
        get
        {
            var vScreen = UIScreen.MainScreen;
            if (vScreen is null)
            {
                return 1.0;
            }

            var vWidthPoints = (double)vScreen.Bounds.Width;
            if (vWidthPoints <= 0)
            {
                return 1.0;
            }

            return Math.Clamp(Math.Round(vWidthPoints / ReferenceWidthPoints, 1), 1.0, MaxZoom);
        }
    }

    /// <summary>
    /// Installs the zoom controller into the BlazorWebView's underlying WKWebView. Runs at
    /// document-start on every load, so the zoom is present as the Blazor content first renders and
    /// the keyboard shortcuts are always bound.
    /// </summary>
    /// <param name="aWebView">The WKWebView hosting the Blazor UI.</param>
    public static void Attach(WKWebView aWebView)
    {
        ArgumentNullException.ThrowIfNull(aWebView);
        var vScript = new WKUserScript(
            new NSString(BuildControllerScript()),
            WKUserScriptInjectionTime.AtDocumentStart,
            isForMainFrameOnly: true);
        aWebView.Configuration.UserContentController.AddUserScript(vScript);
    }

    /// <summary>
    /// Builds the in-page controller: applies the persisted-or-baseline zoom and binds
    /// Cmd/Ctrl +/-/0. The native-computed <see cref="Baseline"/> and clamp bounds are baked in as
    /// invariant-formatted literals.
    /// </summary>
    /// <returns>A self-contained JavaScript IIFE.</returns>
    private static string BuildControllerScript()
    {
        var vDefault = Baseline.ToString(CultureInfo.InvariantCulture);
        var vMin = MinZoom.ToString(CultureInfo.InvariantCulture);
        var vMax = MaxZoom.ToString(CultureInfo.InvariantCulture);

        return $$"""
(function () {
  var KEY = 'trstudio-desktop-zoom';
  var DEF = {{vDefault}}, MIN = {{vMin}}, MAX = {{vMax}};
  function clamp(z) { return Math.min(MAX, Math.max(MIN, z)); }
  var stored = parseFloat(localStorage.getItem(KEY));
  var zoom = clamp(isNaN(stored) ? DEF : stored);
  function apply() {
    var el = document.documentElement;
    if (el) { el.style.zoom = zoom; el.style.setProperty('--tr-zoom', zoom); }
  }
  apply();
  if (!window.__trZoomBound) {
    window.__trZoomBound = true;
    window.addEventListener('keydown', function (ev) {
      if ((!ev.metaKey && !ev.ctrlKey) || ev.altKey) { return; }
      var k = ev.key;
      if (k === '=' || k === '+') { zoom = clamp(Math.round((zoom + 0.1) * 100) / 100); }
      else if (k === '-' || k === '_') { zoom = clamp(Math.round((zoom - 0.1) * 100) / 100); }
      else if (k === '0') { zoom = DEF; }
      else { return; }
      ev.preventDefault();
      try { localStorage.setItem(KEY, zoom); } catch (e) {}
      apply();
    }, true);
  }
})();
""";
    }
}
