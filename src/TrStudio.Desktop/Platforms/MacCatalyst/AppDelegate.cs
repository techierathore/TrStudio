using Foundation;

namespace TrStudio.Desktop;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	// View › Zoom In, Zoom Out, Actual Size (coding-standards-dotnet.md §9b; before macOS 26 only, see BlazorZoom)
	public override void BuildMenu(UIKit.IUIMenuBuilder builder)
	{
		base.BuildMenu(builder);
		BlazorZoom.BuildMenu(builder);
	}

	[Export("tfZoomIn:")]
	public void ZoomIn(NSObject aSender) => BlazorZoom.ZoomBy(1);

	[Export("tfZoomOut:")]
	public void ZoomOut(NSObject aSender) => BlazorZoom.ZoomBy(-1);

	[Export("tfZoomReset:")]
	public void ZoomReset(NSObject aSender) => BlazorZoom.Reset();
}
