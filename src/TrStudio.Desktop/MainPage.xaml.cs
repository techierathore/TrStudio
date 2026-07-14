#if MACCATALYST
using Microsoft.AspNetCore.Components.WebView;
using TrStudio.Desktop.Platforms.MacCatalyst;
#endif

namespace TrStudio.Desktop;

/// <summary>
/// Hosts the BlazorWebView that renders the shared TrStudioUI shell. On Mac Catalyst it also wires
/// browser-style page zoom (REQ-FN-072 UAT): the <c>WebViewZoom</c> controller is installed into the
/// WebView on init, applying the DPI-corrected default and binding Cmd+= / Cmd+- / Cmd+0 (zoom in /
/// out / actual size) in-page, with the chosen level persisted and restored on startup. (Native
/// MAUI menu bar items don't render on this Catalyst build, so the shortcuts live in the WebView.)
/// </summary>
public partial class MainPage : ContentPage
{
	/// <summary>Initialises the page and, on Mac Catalyst, the WebView zoom hook.</summary>
	public MainPage()
	{
		InitializeComponent();
#if MACCATALYST
		blazorWebView.BlazorWebViewInitialized += HandleBlazorWebViewInitialized;
#endif
	}

#if MACCATALYST
	/// <summary>Installs the zoom controller into the underlying WKWebView once it exists.</summary>
	/// <param name="aSender">The BlazorWebView raising the event.</param>
	/// <param name="aArgs">Platform event args exposing the WKWebView.</param>
	private void HandleBlazorWebViewInitialized(object? aSender, BlazorWebViewInitializedEventArgs aArgs)
	{
		WebViewZoom.Attach(aArgs.WebView);
	}
#endif
}
