using System.Runtime.InteropServices;
using TrStudio.Engine.Infrastructure;

namespace TrStudio.Tests.Unit.Desktop;

/// <summary>
/// REQ-FN-010 (owner run of 17 Nov 2026, real Mac app): the engine inside the Mac app is a Mac Catalyst
/// process, where .NET does not report "macOS"; guided setup must still pick the Mac Python build and
/// Hardware &amp; Setup must still probe the Apple GPU.
/// </summary>
public class HostPlatformTests
{
    [Fact(DisplayName = "REQ-FN-010 MacCatalystCountsAsMacForTheManagedPython")]
    public void MacCatalystCountsAsMacForTheManagedPython()
    {
        // a Mac, whichever way .NET names it, gets the Apple build
        Assert.Equal("aarch64-apple-darwin", HostPlatform.PythonTriple(true, false, false, Architecture.Arm64));
        Assert.Equal("x86_64-apple-darwin", HostPlatform.PythonTriple(true, false, false, Architecture.X64));
        Assert.Equal("x86_64-unknown-linux-gnu", HostPlatform.PythonTriple(false, true, false, Architecture.X64));
        Assert.Equal("aarch64-pc-windows-msvc", HostPlatform.PythonTriple(false, false, true, Architecture.Arm64));
        Assert.Null(HostPlatform.PythonTriple(false, false, false, Architecture.Arm64));

        // IsMac is true for either Mac process kind
        Assert.Equal(OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst(), HostPlatform.IsMac);
    }
}
