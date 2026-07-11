using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

// FlaUI-based native-window smoke for the MAUI Blazor Hybrid Windows head. Binds to the app it
// LAUNCHES (its own PID -> top-level window), reads the app window's UIA element tree element by
// element, and captures a screenshot of the app's own window. NO global keyboard/mouse injection —
// the role/route is driven entirely by the app's DEBUG dev-session env vars (verify-phase.md 3b).

static string Arg(string[] a, string k, string d = "")
{
    for (var i = 0; i < a.Length - 1; i++)
    {
        if (a[i] == k) return a[i + 1];
    }
    return d;
}

var exe = Arg(args, "--exe");
var role = Arg(args, "--role", "Owner");
var startPath = Arg(args, "--start");
var apiBase = Arg(args, "--api", "http://localhost:5102");
var shot = Arg(args, "--shot", "shot.png");
var outJson = Arg(args, "--out", "out.json");
var waitToken = Arg(args, "--wait", "TrStudio");

var psi = new ProcessStartInfo
{
    FileName = exe,
    UseShellExecute = false,
    WorkingDirectory = Path.GetDirectoryName(exe)
};
psi.EnvironmentVariables["TrStudioDevRole"] = role;
psi.EnvironmentVariables["TrStudioApiBaseUrl"] = apiBase;
if (!string.IsNullOrEmpty(startPath))
{
    psi.EnvironmentVariables["TrStudioDevStartPath"] = startPath;
}

Console.WriteLine($"[smoke] launching {exe} role={role} start={startPath}");

using var automation = new UIA3Automation();
var app = Application.Launch(psi);
var result = new Dictionary<string, object?>
{
    ["role"] = role,
    ["startPath"] = startPath,
    ["pid"] = app.ProcessId
};

try
{
    Window? window = null;
    for (var i = 0; i < 40 && window is null; i++)
    {
        try { window = app.GetMainWindow(automation, TimeSpan.FromSeconds(2)); } catch { }
        if (window is null) Thread.Sleep(1000);
    }

    if (window is null)
    {
        result["error"] = "no main window";
        File.WriteAllText(outJson, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("[smoke] FAIL: no main window");
        return 2;
    }

    result["windowTitle"] = window.Title;
    try { window.SetForeground(); } catch { }

    // Poll the app window's own UIA subtree until the WebView2 content shows the wait token
    // (the Blazor screen has rendered), up to ~45s.
    var names = new List<string>();
    var found = false;
    for (var i = 0; i < 45 && !found; i++)
    {
        Thread.Sleep(1000);
        names = CollectNames(window);
        found = names.Any(n => n.Contains(waitToken, StringComparison.OrdinalIgnoreCase));
    }

    // Final, settled read.
    Thread.Sleep(1500);
    names = CollectNames(window);

    result["waitToken"] = waitToken;
    result["waitTokenFound"] = found || names.Any(n => n.Contains(waitToken, StringComparison.OrdinalIgnoreCase));
    result["elementCount"] = names.Count;
    result["texts"] = names;

    try
    {
        // PrintWindow(PW_RENDERFULLCONTENT) renders the app window's OWN surface — including the
        // WebView2 (DirectComposition) content — into a bitmap regardless of z-order/occlusion, so
        // the screenshot is the app window itself, not whatever pixels sit at its screen rectangle.
        var hwnd = window.Properties.NativeWindowHandle.Value;
        Native.GetWindowRect(hwnd, out var r);
        var w = Math.Max(1, r.Right - r.Left);
        var h = Math.Max(1, r.Bottom - r.Top);
        using var bmp = new Bitmap(w, h);
        using (var g = Graphics.FromImage(bmp))
        {
            var hdc = g.GetHdc();
            Native.PrintWindow(hwnd, hdc, 0x00000002);
            g.ReleaseHdc(hdc);
        }
        bmp.Save(shot, ImageFormat.Png);
        result["screenshot"] = shot;
    }
    catch (Exception ex)
    {
        result["screenshotError"] = ex.Message;
    }

    File.WriteAllText(outJson, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"[smoke] window='{window.Title}' elements={names.Count} waitTokenFound={result["waitTokenFound"]}");
    return 0;
}
finally
{
    try { app.Close(); } catch { }
    try { if (!app.HasExited) app.Kill(); } catch { }
}

static List<string> CollectNames(AutomationElement root)
{
    var acc = new List<string>();
    var seen = new HashSet<string>();
    void Walk(AutomationElement el, int depth)
    {
        if (depth > 40) return;
        try
        {
            var name = el.Name;
            if (!string.IsNullOrWhiteSpace(name) && seen.Add(name))
            {
                acc.Add(name);
            }
        }
        catch { }
        AutomationElement[] kids;
        try { kids = el.FindAllChildren(); } catch { return; }
        foreach (var k in kids) Walk(k, depth + 1);
    }
    Walk(root, 0);
    return acc;
}

static partial class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
}
