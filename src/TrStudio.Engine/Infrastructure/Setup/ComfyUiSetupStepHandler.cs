using System.IO.Compression;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Install;
using TrStudio.Engine.Domain.Setup;

namespace TrStudio.Engine.Infrastructure.Setup;

/// <summary>
/// Guided-setup step 3 (REQ-FN-009/010): provisions managed ComfyUI — downloads the pinned
/// release ZIP over HTTPS (codeload, no git), unpacks it into <c>{managedRoot}/comfyui</c> and
/// installs its pip dependencies into the ISOLATED Python environment (<c>--no-user</c> +
/// <c>PYTHONNOUSERSITE=1</c>, so the system Python and user site are never touched).
/// </summary>
public class ComfyUiSetupStepHandler : ISetupStepHandler
{
    private const string VersionMarkerFileName = ".trstudio-version";

    private readonly SetupOptions objOptions;
    private readonly SetupPaths objPaths;
    private readonly IInstallDownloader objDownloader;
    private readonly SetupProcessRunner objProcessRunner;
    private readonly ILogger<ComfyUiSetupStepHandler> objLogger;

    /// <summary>
    /// Initialises the handler.
    /// </summary>
    /// <param name="aOptions">Setup options carrying the ComfyUI release pin.</param>
    /// <param name="aPaths">The managed-environment path resolver.</param>
    /// <param name="aDownloader">Resumable downloader for the release ZIP.</param>
    /// <param name="aProcessRunner">Runner used to invoke the isolated Python's pip.</param>
    /// <param name="aLogger">Logger for provisioning diagnostics.</param>
    public ComfyUiSetupStepHandler(
        SetupOptions aOptions,
        SetupPaths aPaths,
        IInstallDownloader aDownloader,
        SetupProcessRunner aProcessRunner,
        ILogger<ComfyUiSetupStepHandler> aLogger)
    {
        objOptions = aOptions;
        objPaths = aPaths;
        objDownloader = aDownloader;
        objProcessRunner = aProcessRunner;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public string StepKey => SetupStepKeys.ComfyUi;

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<SetupStepProgress> aProgress, CancellationToken aCancellationToken)
    {
        RequireIsolatedPython();

        if (IsAlreadyProvisioned())
        {
            aProgress.Report(new SetupStepProgress(100, $"ComfyUI {objOptions.ComfyUiVersionTag} already provisioned"));
            return;
        }

        var vZipPath = await DownloadReleaseZipAsync(aProgress, aCancellationToken).ConfigureAwait(false);

        aProgress.Report(new SetupStepProgress(45, "Unpacking ComfyUI into the managed environment"));
        ExtractIntoManagedRoot(vZipPath, aCancellationToken);

        await InstallPipDependenciesAsync(aProgress, aCancellationToken).ConfigureAwait(false);

        File.WriteAllText(Path.Combine(objPaths.ComfyUiDir, VersionMarkerFileName), objOptions.ComfyUiVersionTag);
        TryDelete(vZipPath);
        aProgress.Report(new SetupStepProgress(100, $"ComfyUI {objOptions.ComfyUiVersionTag} provisioned"));
    }

    private void RequireIsolatedPython()
    {
        if (!File.Exists(objPaths.PythonExecutable))
        {
            throw new SetupStepException(
                "The isolated Python environment is not provisioned yet. " +
                "Run setup step 2 ('Provision isolated Python') first, then re-run this step.");
        }
    }

    private bool IsAlreadyProvisioned()
    {
        var vMarkerPath = Path.Combine(objPaths.ComfyUiDir, VersionMarkerFileName);
        return File.Exists(Path.Combine(objPaths.ComfyUiDir, "main.py"))
            && File.Exists(vMarkerPath)
            && File.ReadAllText(vMarkerPath).Trim() == objOptions.ComfyUiVersionTag;
    }

    private async Task<string> DownloadReleaseZipAsync(IProgress<SetupStepProgress> aProgress, CancellationToken aCancellationToken)
    {
        var vTargetPath = Path.Combine(objPaths.DownloadsDir, $"comfyui-{objOptions.ComfyUiVersionTag}.zip");
        if (File.Exists(vTargetPath))
        {
            return vTargetPath;
        }

        Directory.CreateDirectory(objPaths.DownloadsDir);
        var vProgressSink = new Progress<InstallDownloadProgress>(aSample => aProgress.Report(MapDownloadProgress(aSample)));
        try
        {
            await objDownloader.DownloadAsync(
                new InstallDownloadRequest { Url = objOptions.ComfyUiZipUrl, TargetFilePath = vTargetPath },
                vProgressSink,
                aCancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException vException)
        {
            throw new SetupStepException(
                $"Downloading the ComfyUI {objOptions.ComfyUiVersionTag} release ZIP failed ({vException.StatusCode?.ToString() ?? "network error"}). " +
                "Check the internet connection (github.com must be reachable) and re-run the step — the download resumes where it stopped.",
                vException);
        }

        return vTargetPath;
    }

    private void ExtractIntoManagedRoot(string aZipPath, CancellationToken aCancellationToken)
    {
        var vExtractDir = Path.Combine(objPaths.Root, "tmp", "comfyui-extract");
        try
        {
            if (Directory.Exists(vExtractDir))
            {
                Directory.Delete(vExtractDir, recursive: true);
            }

            Directory.CreateDirectory(vExtractDir);
            ZipFile.ExtractToDirectory(aZipPath, vExtractDir, overwriteFiles: true);

            aCancellationToken.ThrowIfCancellationRequested();
            var vInnerDir = Directory.GetDirectories(vExtractDir).FirstOrDefault()
                ?? throw new SetupStepException(
                    "The ComfyUI release ZIP was empty. Verify Setup:ComfyUiZipUrl points at a codeload ZIP for a valid tag.");

            if (Directory.Exists(objPaths.ComfyUiDir))
            {
                Directory.Delete(objPaths.ComfyUiDir, recursive: true);
            }

            Directory.Move(vInnerDir, objPaths.ComfyUiDir);
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
                $"Unpacking ComfyUI into '{objPaths.ComfyUiDir}' failed: {vException.Message}. " +
                "Check free disk space and directory permissions under the managed root, then re-run the step.",
                vException);
        }
        finally
        {
            TryDeleteDirectory(vExtractDir);
        }
    }

    private async Task InstallPipDependenciesAsync(IProgress<SetupStepProgress> aProgress, CancellationToken aCancellationToken)
    {
        var vRequirementsPath = Path.Combine(objPaths.ComfyUiDir, "requirements.txt");
        if (!File.Exists(vRequirementsPath))
        {
            throw new SetupStepException(
                "ComfyUI's requirements.txt was not found after unpacking. " +
                "Verify Setup:ComfyUiZipUrl points at an official ComfyUI release ZIP and re-run the step.");
        }

        var vEnvironment = new Dictionary<string, string>
        {
            ["PYTHONNOUSERSITE"] = "1",
            ["PIP_DISABLE_PIP_VERSION_CHECK"] = "1"
        };
        var vLineCount = 0;
        aProgress.Report(new SetupStepProgress(50, "Installing ComfyUI Python dependencies into the isolated environment"));
        var vExitCode = await objProcessRunner.RunAsync(
            objPaths.PythonExecutable,
            new[] { "-m", "pip", "install", "--no-user", "--no-input", "-r", vRequirementsPath },
            vEnvironment,
            aLine => ReportPipProgress(aProgress, aLine, ref vLineCount),
            aCancellationToken).ConfigureAwait(false);
        if (vExitCode != 0)
        {
            throw new SetupStepException(
                $"Installing ComfyUI's Python dependencies failed (pip exit {vExitCode}). " +
                "Check the internet connection and free disk space, then re-run the step — already-installed packages are skipped.");
        }
    }

    private void ReportPipProgress(IProgress<SetupStepProgress> aProgress, string aLine, ref int aLineCount)
    {
        aLineCount++;
        var vPercent = Math.Min(99, 50 + (aLineCount / 4));
        aProgress.Report(new SetupStepProgress(vPercent, $"pip: {Truncate(aLine, 120)}"));
        objLogger.LogDebug("pip output: {Line}", aLine);
    }

    private static string Truncate(string aValue, int aMaxLength)
        => aValue.Length <= aMaxLength ? aValue : aValue[..aMaxLength];

    private static SetupStepProgress MapDownloadProgress(InstallDownloadProgress aSample)
    {
        var vPercentOfDownload = aSample.TotalBytes is > 0
            ? (int)(aSample.DownloadedBytes * 100 / aSample.TotalBytes.Value)
            : 0;
        var vOverall = 5 + (vPercentOfDownload * 40 / 100);
        var vDownloadedMb = aSample.DownloadedBytes / (1024d * 1024d);
        return new SetupStepProgress(vOverall, $"Downloading ComfyUI ({vDownloadedMb:F1} MB)");
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
