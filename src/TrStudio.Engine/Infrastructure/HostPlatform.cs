using System.Runtime.InteropServices;

namespace TrStudio.Engine.Infrastructure;

/// <summary>
/// Which computer the engine runs on. Inside the Mac app the engine runs as a Mac Catalyst process, and
/// .NET then answers false to <see cref="OperatingSystem.IsMacOS"/> and to
/// <c>RuntimeInformation.IsOSPlatform(OSPlatform.OSX)</c>; the same Mac must still count as a Mac
/// (owner run of 17 Nov 2026: guided setup refused "Mac Catalyst (Arm64)" as an unsupported host, and
/// Hardware &amp; Setup said there was no Apple GPU).
/// </summary>
public static class HostPlatform
{
    /// <summary>True on a Mac, whether the engine runs as a macOS or a Mac Catalyst process.</summary>
    public static bool IsMac => OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst();

    /// <summary>
    /// The python-build-standalone target for a platform and processor, or null when there is none.
    /// </summary>
    /// <param name="aIsMac">A Mac (macOS or Mac Catalyst).</param>
    /// <param name="aIsLinux">Linux.</param>
    /// <param name="aIsWindows">Windows.</param>
    /// <param name="aArchitecture">The processor.</param>
    /// <returns>The target triple, e.g. <c>aarch64-apple-darwin</c>.</returns>
    public static string? PythonTriple(bool aIsMac, bool aIsLinux, bool aIsWindows, Architecture aArchitecture)
    {
        var vArm = aArchitecture == Architecture.Arm64;
        if (aIsMac)
        {
            return vArm ? "aarch64-apple-darwin" : "x86_64-apple-darwin";
        }

        if (aIsLinux)
        {
            return vArm ? "aarch64-unknown-linux-gnu" : "x86_64-unknown-linux-gnu";
        }

        if (aIsWindows)
        {
            return vArm ? "aarch64-pc-windows-msvc" : "x86_64-pc-windows-msvc";
        }

        return null;
    }
}
