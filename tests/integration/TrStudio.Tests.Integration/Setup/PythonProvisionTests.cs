using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TrStudio.Engine.Domain.Install;
using TrStudio.Engine.Domain.Setup;
using TrStudio.Engine.Infrastructure.Setup;
using TrStudio.Engine.Workers.Install;

namespace TrStudio.Tests.Integration.Setup;

/// <summary>
/// REQ-FN-010 (BRD-10): provisioning the managed Python environment is real here, not a stand-in. The
/// engine's own Python step downloads python-build-standalone (about 30 MB, from github.com) into a
/// managed root inside a temporary folder, and the test proves that only that root changed: a sibling
/// folder beside it is untouched, and the usual places a system or user Python lives are unchanged.
/// The temporary folder is deleted at the end, so nothing stays installed on this computer.
/// </summary>
public class PythonProvisionTests
{
    /// <summary>Places a Python on this Mac may live outside the managed root; none may change.</summary>
    private static readonly string[] SystemPythonPlaces =
    {
        "/usr/bin/python3",
        "/usr/local/bin/python3",
        "/opt/homebrew/bin/python3",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Python"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pyenv"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin"),
    };

    [Fact(DisplayName = "REQ-FN-010 PythonProvisionTouchesOnlyManagedPath")]
    public async Task PythonProvisionTouchesOnlyManagedPath()
    {
        var vParent = Path.Combine(Path.GetTempPath(), "trstudio-fn010-" + Guid.NewGuid().ToString("N")[..8]);
        var vManaged = Path.Combine(vParent, "managed");
        var vOutside = Path.Combine(vParent, "outside");
        Directory.CreateDirectory(vOutside);
        await File.WriteAllTextAsync(Path.Combine(vOutside, "keep.txt"), "not part of the managed environment");
        try
        {
            var vOutsideBefore = Snapshot(vOutside);
            var vSystemBefore = SystemPythonPlaces.ToDictionary(aPlace => aPlace, Stamp);

            var vOptions = new SetupOptions { ManagedEnvRoot = vManaged };
            var vPaths = new SetupPaths(vOptions);
            using var vServices = new ServiceCollection().AddHttpClient().BuildServiceProvider();
            var vHandler = new PythonEnvSetupStepHandler(
                vOptions,
                vPaths,
                new InstallDownloader(vServices.GetRequiredService<IHttpClientFactory>(), NullLogger<InstallDownloader>.Instance),
                new SetupProcessRunner(NullLogger<SetupProcessRunner>.Instance),
                NullLogger<PythonEnvSetupStepHandler>.Instance);

            var vMessages = new List<string>();
            await vHandler.ExecuteAsync(new Progress<SetupStepProgress>(aStep => vMessages.Add(aStep.Message ?? string.Empty)), CancellationToken.None);

            // The interpreter is inside the managed root, runs, and its prefix is the managed root.
            Assert.StartsWith(Path.GetFullPath(vManaged), Path.GetFullPath(vPaths.PythonExecutable));
            Assert.True(File.Exists(vPaths.PythonExecutable), "the managed interpreter exists");
            var vPrefix = Run(vPaths.PythonExecutable, "-c \"import sys; print(sys.version.split()[0]); print(sys.prefix)\"");
            Assert.StartsWith(vOptions.PythonVersion, vPrefix[0]);
            Assert.StartsWith(Resolve(vPaths.PythonDir), Resolve(vPrefix[1]));

            // Nothing outside the managed root changed.
            Assert.Equal(vOutsideBefore, Snapshot(vOutside));
            Assert.Equal(new[] { "managed", "outside" }, Directory.GetFileSystemEntries(vParent).Select(Path.GetFileName).OrderBy(aName => aName).ToArray());
            foreach (var vPlace in SystemPythonPlaces)
            {
                Assert.True(vSystemBefore[vPlace] == Stamp(vPlace), $"{vPlace} changed");
            }

            // The lifecycle step is safe to run again: it sees the provisioned interpreter and stops.
            var vAgain = new List<string>();
            await vHandler.ExecuteAsync(new Progress<SetupStepProgress>(aStep => vAgain.Add(aStep.Message ?? string.Empty)), CancellationToken.None);
            await Task.Delay(200);
            Assert.Contains(vAgain, aMessage => aMessage.Contains("already provisioned", StringComparison.Ordinal));
        }
        finally
        {
            try
            {
                Directory.Delete(vParent, recursive: true);
            }
            catch (IOException)
            {
                // a temporary folder; the system cleans it if a file is still held
            }
        }
    }

    private static string Resolve(string aPath)
    {
        var vFull = Path.GetFullPath(aPath.Trim());
        // /var is a link to /private/var on macOS; compare the resolved forms.
        return vFull.StartsWith("/var/", StringComparison.Ordinal) ? "/private" + vFull : vFull;
    }

    private static string Stamp(string aPath)
    {
        if (File.Exists(aPath))
        {
            var vInfo = new FileInfo(aPath);
            return $"file {vInfo.Length} {vInfo.LastWriteTimeUtc:O}";
        }

        return Directory.Exists(aPath) ? $"dir {Directory.GetLastWriteTimeUtc(aPath):O}" : "absent";
    }

    private static string[] Snapshot(string aDir)
        => Directory.GetFiles(aDir, "*", SearchOption.AllDirectories)
            .Select(aFile => $"{Path.GetRelativePath(aDir, aFile)} {new FileInfo(aFile).Length} {File.GetLastWriteTimeUtc(aFile):O}")
            .OrderBy(aLine => aLine, StringComparer.Ordinal)
            .ToArray();

    private static string[] Run(string aFile, string aArguments)
    {
        using var vProcess = Process.Start(new ProcessStartInfo(aFile, aArguments) { RedirectStandardOutput = true, UseShellExecute = false })!;
        var vOutput = vProcess.StandardOutput.ReadToEnd();
        vProcess.WaitForExit();
        Assert.Equal(0, vProcess.ExitCode);
        return vOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }
}
