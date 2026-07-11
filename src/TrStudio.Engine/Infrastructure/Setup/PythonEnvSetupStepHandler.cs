using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Install;
using TrStudio.Engine.Domain.Setup;

namespace TrStudio.Engine.Infrastructure.Setup;

/// <summary>
/// Guided-setup step 2 (REQ-FN-009/010): provisions the ISOLATED Python environment from the
/// python-build-standalone project — downloads the right tarball for this OS/arch over HTTPS
/// (resumable), unpacks it into <c>{managedRoot}/python</c> and verifies the interpreter runs.
/// System Python is NEVER touched; re-provisioning replaces only the managed directory.
/// </summary>
public class PythonEnvSetupStepHandler : ISetupStepHandler
{
    private readonly SetupOptions objOptions;
    private readonly SetupPaths objPaths;
    private readonly IInstallDownloader objDownloader;
    private readonly SetupProcessRunner objProcessRunner;
    private readonly ILogger<PythonEnvSetupStepHandler> objLogger;

    /// <summary>
    /// Initialises the handler.
    /// </summary>
    /// <param name="aOptions">Setup options carrying the python-build-standalone pins.</param>
    /// <param name="aPaths">The managed-environment path resolver.</param>
    /// <param name="aDownloader">Resumable downloader for the tarball.</param>
    /// <param name="aProcessRunner">Runner used to verify the provisioned interpreter.</param>
    /// <param name="aLogger">Logger for provisioning diagnostics.</param>
    public PythonEnvSetupStepHandler(
        SetupOptions aOptions,
        SetupPaths aPaths,
        IInstallDownloader aDownloader,
        SetupProcessRunner aProcessRunner,
        ILogger<PythonEnvSetupStepHandler> aLogger)
    {
        objOptions = aOptions;
        objPaths = aPaths;
        objDownloader = aDownloader;
        objProcessRunner = aProcessRunner;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public string StepKey => SetupStepKeys.PythonEnv;

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<SetupStepProgress> aProgress, CancellationToken aCancellationToken)
    {
        if (await IsAlreadyProvisionedAsync(aCancellationToken).ConfigureAwait(false))
        {
            aProgress.Report(new SetupStepProgress(100, $"Isolated CPython {objOptions.PythonVersion} already provisioned"));
            return;
        }

        var vTarballPath = await DownloadTarballAsync(aProgress, aCancellationToken).ConfigureAwait(false);

        aProgress.Report(new SetupStepProgress(65, "Unpacking CPython into the managed environment"));
        ExtractIntoManagedRoot(vTarballPath, aCancellationToken);

        aProgress.Report(new SetupStepProgress(90, "Verifying the isolated interpreter"));
        await VerifyInterpreterAsync(aCancellationToken).ConfigureAwait(false);

        TryDelete(vTarballPath);
        aProgress.Report(new SetupStepProgress(100, $"Isolated CPython {objOptions.PythonVersion} provisioned"));
    }

    private async Task<bool> IsAlreadyProvisionedAsync(CancellationToken aCancellationToken)
    {
        if (!File.Exists(objPaths.PythonExecutable))
        {
            return false;
        }

        var vVersionOutput = string.Empty;
        var vExitCode = await objProcessRunner.RunAsync(
            objPaths.PythonExecutable,
            new[] { "--version" },
            null,
            aLine => vVersionOutput += aLine,
            aCancellationToken).ConfigureAwait(false);
        return vExitCode == 0 && vVersionOutput.Contains(objOptions.PythonVersion, StringComparison.Ordinal);
    }

    private async Task<string> DownloadTarballAsync(IProgress<SetupStepProgress> aProgress, CancellationToken aCancellationToken)
    {
        var vTriple = ResolvePlatformTriple();
        var vFileName = $"cpython-{objOptions.PythonVersion}+{objOptions.PythonStandaloneTag}-{vTriple}-install_only.tar.gz";
        var vUrl = $"{objOptions.PythonStandaloneBaseUrl.TrimEnd('/')}/{objOptions.PythonStandaloneTag}/{vFileName}";
        var vTargetPath = Path.Combine(objPaths.DownloadsDir, vFileName);

        if (File.Exists(vTargetPath))
        {
            return vTargetPath;
        }

        Directory.CreateDirectory(objPaths.DownloadsDir);
        var vProgressSink = new Progress<InstallDownloadProgress>(aSample => aProgress.Report(MapDownloadProgress(aSample)));
        try
        {
            await objDownloader.DownloadAsync(
                new InstallDownloadRequest { Url = vUrl, TargetFilePath = vTargetPath },
                vProgressSink,
                aCancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException vException)
        {
            throw new SetupStepException(
                $"Downloading CPython {objOptions.PythonVersion} from python-build-standalone failed ({vException.StatusCode?.ToString() ?? "network error"}). " +
                "Check the internet connection (github.com must be reachable) and re-run the step — the download resumes where it stopped.",
                vException);
        }

        return vTargetPath;
    }

    private void ExtractIntoManagedRoot(string aTarballPath, CancellationToken aCancellationToken)
    {
        var vExtractDir = Path.Combine(objPaths.Root, "tmp", "python-extract");
        try
        {
            if (Directory.Exists(vExtractDir))
            {
                Directory.Delete(vExtractDir, recursive: true);
            }

            Directory.CreateDirectory(vExtractDir);
            using (var vFileStream = File.OpenRead(aTarballPath))
            using (var vGzipStream = new GZipStream(vFileStream, CompressionMode.Decompress))
            {
                TarFile.ExtractToDirectory(vGzipStream, vExtractDir, overwriteFiles: true);
            }

            aCancellationToken.ThrowIfCancellationRequested();
            var vUnpackedPythonDir = Path.Combine(vExtractDir, "python");
            if (!Directory.Exists(vUnpackedPythonDir))
            {
                throw new SetupStepException(
                    "The python-build-standalone archive did not contain the expected 'python' folder. " +
                    "Verify Setup:PythonStandaloneTag/Setup:PythonVersion point at an install_only build.");
            }

            if (Directory.Exists(objPaths.PythonDir))
            {
                Directory.Delete(objPaths.PythonDir, recursive: true);
            }

            Directory.Move(vUnpackedPythonDir, objPaths.PythonDir);
        }
        catch (SetupStepException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception vException)
        {
            throw new SetupStepException(
                $"Unpacking CPython into '{objPaths.PythonDir}' failed: {vException.Message}. " +
                "Check free disk space and directory permissions under the managed root, then re-run the step.",
                vException);
        }
        finally
        {
            TryDeleteDirectory(vExtractDir);
        }
    }

    private async Task VerifyInterpreterAsync(CancellationToken aCancellationToken)
    {
        var vOutput = string.Empty;
        var vExitCode = await objProcessRunner.RunAsync(
            objPaths.PythonExecutable,
            new[] { "--version" },
            null,
            aLine => vOutput += aLine,
            aCancellationToken).ConfigureAwait(false);
        if (vExitCode != 0)
        {
            throw new SetupStepException(
                $"The provisioned interpreter at '{objPaths.PythonExecutable}' failed to run (exit {vExitCode}). " +
                "Re-run the step to re-provision the managed Python environment.");
        }

        objLogger.LogInformation("Isolated Python verified: {Version}", vOutput);
    }

    private string ResolvePlatformTriple()
    {
        var vArchitecture = RuntimeInformation.OSArchitecture;
        if (OperatingSystem.IsMacOS())
        {
            return vArchitecture == Architecture.Arm64 ? "aarch64-apple-darwin" : "x86_64-apple-darwin";
        }

        if (OperatingSystem.IsLinux())
        {
            return vArchitecture == Architecture.Arm64 ? "aarch64-unknown-linux-gnu" : "x86_64-unknown-linux-gnu";
        }

        if (OperatingSystem.IsWindows())
        {
            return vArchitecture == Architecture.Arm64 ? "aarch64-pc-windows-msvc" : "x86_64-pc-windows-msvc";
        }

        throw new SetupStepException(
            $"Unsupported host platform '{RuntimeInformation.OSDescription} ({vArchitecture})' — " +
            "the isolated Python environment supports macOS, Linux and Windows on x64/arm64.");
    }

    private static SetupStepProgress MapDownloadProgress(InstallDownloadProgress aSample)
    {
        var vPercentOfDownload = aSample.TotalBytes is > 0
            ? (int)(aSample.DownloadedBytes * 100 / aSample.TotalBytes.Value)
            : 0;
        var vOverall = 5 + (vPercentOfDownload * 55 / 100);
        return new SetupStepProgress(vOverall, $"Downloading CPython ({vPercentOfDownload}%)");
    }

    private void TryDelete(string aPath)
    {
        try
        {
            File.Delete(aPath);
        }
        catch (Exception vException)
        {
            objLogger.LogDebug(vException, "Could not delete staging file {Path}", aPath);
        }
    }

    private void TryDeleteDirectory(string aPath)
    {
        try
        {
            if (Directory.Exists(aPath))
            {
                Directory.Delete(aPath, recursive: true);
            }
        }
        catch (Exception vException)
        {
            objLogger.LogDebug(vException, "Could not delete staging directory {Path}", aPath);
        }
    }
}
