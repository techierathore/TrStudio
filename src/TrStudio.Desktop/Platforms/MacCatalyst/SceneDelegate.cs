using Foundation;

namespace TrStudio.Desktop;

// macOS 27 quits an app at its first window unless Info.plist names a scene delegate (coding-standards-dotnet.md §9)
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
