#if MACCATALYST
using UIKit;
#endif

namespace TrStudio.Desktop;

/// <summary>
/// Hosts the BlazorWebView that renders the shared TrStudioUI shell, with OS zoom (REQ-FN-072 UAT;
/// coding-standards-dotnet.md §9b): Cmd/Ctrl+= in, Cmd/Ctrl+- out, Cmd/Ctrl+0 actual size, 50% to 300%,
/// the level kept across restarts. On a Mac, before anyone has zoomed, the app opens at a level sized to
/// the screen (about 160% on a 4K panel driven at native resolution), which the owner chose after UAT.
/// </summary>
public partial class MainPage : ContentPage
{
	/// <summary>The logical point width that opens at 100%. A 4K panel at native resolution (~3840 points)
	/// opens at ~1.6x; a 1080p or HiDPI display (~1920 points) at 1.0.</summary>
	private const double ReferenceWidthPoints = 2400.0;

	/// <summary>Initialises the page and hands the web view to <see cref="BlazorZoom"/>.</summary>
	public MainPage()
	{
		InitializeComponent();
#if MACCATALYST
		BlazorZoom.FirstLevel = ScreenBaseline();
#endif
		blazorWebView.BlazorWebViewInitialized += (aSender, aArgs) => BlazorZoom.Attach(aArgs.WebView);
	}

#if MACCATALYST
	/// <summary>
	/// The first-run level for this display: its logical point width over <see cref="ReferenceWidthPoints"/>,
	/// never below 1.0, at most 3.0. Keyed off point width, not backing scale: a 4K panel at native
	/// resolution reports scale 1.0 yet draws everything tiny. 1.0 when the screen is not known.
	/// </summary>
	/// <returns>The level to open at until one is saved.</returns>
	private static double ScreenBaseline()
	{
		var vWidthPoints = (double)(UIScreen.MainScreen?.Bounds.Width ?? 0);
		return vWidthPoints <= 0 ? 1.0 : Math.Clamp(Math.Round(vWidthPoints / ReferenceWidthPoints, 1), 1.0, BlazorZoom.Max);
	}
#endif
}
