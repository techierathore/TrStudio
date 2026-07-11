using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using TrStudio.Engine.Domain.Setup;

namespace TrStudio.Engine.Infrastructure.Setup;

/// <summary>
/// Guided-setup step 1 (REQ-FN-009): provisions the managed model/cache directories under the
/// managed root and probes that the ONNX Runtime native library loads (ADR-006 image path).
/// Idempotent — re-running re-verifies the directories and the runtime.
/// </summary>
public class OnnxSetupStepHandler : ISetupStepHandler
{
    private readonly SetupPaths objPaths;
    private readonly ILogger<OnnxSetupStepHandler> objLogger;

    /// <summary>
    /// Initialises the handler.
    /// </summary>
    /// <param name="aPaths">The managed-environment path resolver.</param>
    /// <param name="aLogger">Logger for probe diagnostics.</param>
    public OnnxSetupStepHandler(SetupPaths aPaths, ILogger<OnnxSetupStepHandler> aLogger)
    {
        objPaths = aPaths;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public string StepKey => SetupStepKeys.OnnxRuntime;

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<SetupStepProgress> aProgress, CancellationToken aCancellationToken)
    {
        aProgress.Report(new SetupStepProgress(10, "Creating managed directories"));
        CreateManagedDirectories();

        aProgress.Report(new SetupStepProgress(40, "Verifying managed root is writable"));
        VerifyWritable();

        aCancellationToken.ThrowIfCancellationRequested();
        aProgress.Report(new SetupStepProgress(70, "Probing ONNX Runtime"));
        ProbeOnnxRuntime();

        aProgress.Report(new SetupStepProgress(100, "ONNX runtime ready"));
        return Task.CompletedTask;
    }

    private void CreateManagedDirectories()
    {
        try
        {
            Directory.CreateDirectory(objPaths.Root);
            Directory.CreateDirectory(objPaths.ModelsDir);
            Directory.CreateDirectory(objPaths.CacheDir);
            Directory.CreateDirectory(objPaths.OnnxDir);
            Directory.CreateDirectory(objPaths.DownloadsDir);
        }
        catch (Exception vException)
        {
            throw new SetupStepException(
                $"Could not create the managed environment directories under '{objPaths.Root}'. " +
                "Check that the account running TrStudioApi can write to that location, or set Setup:ManagedEnvRoot to a writable path.",
                vException);
        }
    }

    private void VerifyWritable()
    {
        var vProbeFile = Path.Combine(objPaths.Root, ".write-probe");
        try
        {
            File.WriteAllText(vProbeFile, "ok");
            File.Delete(vProbeFile);
        }
        catch (Exception vException)
        {
            throw new SetupStepException(
                $"The managed environment root '{objPaths.Root}' is not writable. " +
                "Fix the directory permissions or point Setup:ManagedEnvRoot at a writable path.",
                vException);
        }
    }

    private void ProbeOnnxRuntime()
    {
        try
        {
            using var vSessionOptions = new SessionOptions();
            objLogger.LogInformation("ONNX Runtime native library loaded successfully");
        }
        catch (Exception vException)
        {
            throw new SetupStepException(
                "The ONNX Runtime native library failed to load on this host. " +
                "Reinstall the application (the runtime ships with it), or verify the OS/architecture is supported (win-x64, linux-x64/arm64, osx-arm64/x64).",
                vException);
        }
    }
}
