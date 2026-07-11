using System.Diagnostics;
using System.Text;

namespace TrStudio.Tests.Integration;

/// <summary>
/// xUnit fixture that boots the real <c>TrStudioApi</c> host as an out-of-process Kestrel
/// server on <c>http://localhost:5102</c> for the duration of the SignalR progress-hub
/// integration test (REQ-FN-005). It runs the already-built <c>TrStudioApi.dll</c> (the
/// solution build produces it) with the dev symmetric-token fallback enabled, waits for the
/// health probe (proving DbUp migration completed and the Hangfire server is running), and
/// tears the process down on disposal so port 5102 is freed for the next run.
/// </summary>
public sealed class LiveApiFixture : IAsyncLifetime
{
    /// <summary>The base address the booted API listens on.</summary>
    public const string BaseAddress = "http://localhost:5102";

    private const string HostPort = "5102";

    private readonly StringBuilder objOutput = new();
    private Process? objProcess;

    /// <summary>An <see cref="HttpClient"/> pointed at the booted API for job submission.</summary>
    public HttpClient Client { get; } = new() { BaseAddress = new Uri(BaseAddress) };

    /// <summary>
    /// Boots the API process and blocks until its <c>/health</c> endpoint reports ready.
    /// </summary>
    /// <returns>A task completing once the API is serving traffic.</returns>
    public async Task InitializeAsync()
    {
        var vApiDll = ResolveApiDllPath();
        var vApiDir = Path.GetDirectoryName(vApiDll)!;

        var vStartInfo = new ProcessStartInfo
        {
            FileName = ResolveDotnetMuxer(),
            WorkingDirectory = vApiDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        vStartInfo.ArgumentList.Add(vApiDll);
        vStartInfo.Environment["ASPNETCORE_URLS"] = BaseAddress;
        vStartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        // Enable the local HS256 dev-token fallback so the test's self-signed bearer token is
        // accepted without a live AppManager (Program.cs: AppManager:AllowDevSymmetricTokens).
        vStartInfo.Environment["AppManager__AllowDevSymmetricTokens"] = "true";
        vStartInfo.Environment["AppManager__UseRemoteTokenValidation"] = "false";

        objProcess = new Process { StartInfo = vStartInfo, EnableRaisingEvents = true };
        objProcess.OutputDataReceived += (_, aArgs) => { if (aArgs.Data is not null) { lock (objOutput) { objOutput.AppendLine(aArgs.Data); } } };
        objProcess.ErrorDataReceived += (_, aArgs) => { if (aArgs.Data is not null) { lock (objOutput) { objOutput.AppendLine(aArgs.Data); } } };

        objProcess.Start();
        objProcess.BeginOutputReadLine();
        objProcess.BeginErrorReadLine();

        await WaitForHealthAsync(TimeSpan.FromSeconds(90)).ConfigureAwait(false);
    }

    /// <summary>
    /// Kills the API process (and its tree) so the listening port is released.
    /// </summary>
    /// <returns>A task completing once the process has exited.</returns>
    public async Task DisposeAsync()
    {
        Client.Dispose();

        if (objProcess is { HasExited: false })
        {
            try
            {
                objProcess.Kill(entireProcessTree: true);
                await objProcess.WaitForExitAsync().ConfigureAwait(false);
            }
            catch
            {
                // The process may already be gone; nothing further to clean up.
            }
        }

        objProcess?.Dispose();
    }

    /// <summary>Returns the captured stdout/stderr of the API process (diagnostics on failure).</summary>
    public string CapturedOutput()
    {
        lock (objOutput)
        {
            return objOutput.ToString();
        }
    }

    private async Task WaitForHealthAsync(TimeSpan aTimeout)
    {
        using var vProbe = new HttpClient { BaseAddress = new Uri(BaseAddress) };
        var vDeadline = DateTime.UtcNow + aTimeout;

        while (DateTime.UtcNow < vDeadline)
        {
            if (objProcess is { HasExited: true })
            {
                throw new InvalidOperationException(
                    $"TrStudioApi exited early (code {objProcess.ExitCode}) before becoming healthy.{Environment.NewLine}{CapturedOutput()}");
            }

            try
            {
                var vResponse = await vProbe.GetAsync("/health").ConfigureAwait(false);
                if (vResponse.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // The listener is not up yet; keep polling until the deadline.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"TrStudioApi did not report healthy on {BaseAddress}/health within {aTimeout.TotalSeconds:0}s.{Environment.NewLine}{CapturedOutput()}");
    }

    private static string ResolveApiDllPath()
    {
        var vTfmDir = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var vConfiguration = vTfmDir.Parent?.Name ?? "Debug";
        var vTfm = vTfmDir.Name;

        var vRepoRoot = FindRepositoryRoot(vTfmDir);
        var vApiDll = Path.Combine(vRepoRoot, "src", "TrStudioApi", "bin", vConfiguration, vTfm, "TrStudioApi.dll");
        if (!File.Exists(vApiDll))
        {
            throw new FileNotFoundException(
                $"Built TrStudioApi.dll not found at '{vApiDll}'. Build the solution first (dotnet build TrStudio.slnx).", vApiDll);
        }

        return vApiDll;
    }

    private static string FindRepositoryRoot(DirectoryInfo aStart)
    {
        for (var vDir = aStart; vDir is not null; vDir = vDir.Parent)
        {
            if (File.Exists(Path.Combine(vDir.FullName, "TrStudio.slnx"))
                || File.Exists(Path.Combine(vDir.FullName, "TrStudio.sln")))
            {
                return vDir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root (TrStudio.slnx) above the test assembly.");
    }

    private static string ResolveDotnetMuxer()
    {
        var vExecutable = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

        var vRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(vRoot))
        {
            var vCandidate = Path.Combine(vRoot, vExecutable);
            if (File.Exists(vCandidate))
            {
                return vCandidate;
            }
        }

        var vProcessPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(vProcessPath)
            && Path.GetFileName(vProcessPath).Equals(vExecutable, StringComparison.OrdinalIgnoreCase))
        {
            return vProcessPath;
        }

        var vHome = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrWhiteSpace(vHome))
        {
            var vHomeCandidate = Path.Combine(vHome, ".dotnet", vExecutable);
            if (File.Exists(vHomeCandidate))
            {
                return vHomeCandidate;
            }
        }

        // Fall back to PATH resolution.
        return vExecutable;
    }
}
