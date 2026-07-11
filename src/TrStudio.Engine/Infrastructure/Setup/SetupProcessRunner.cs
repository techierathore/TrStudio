using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TrStudio.Engine.Infrastructure.Setup;

/// <summary>
/// Runs managed-environment child processes (the isolated Python, pip) with captured output
/// and hard kill on cancellation. Only executables under the managed root are ever launched
/// by the setup cluster (REQ-FN-010).
/// </summary>
public class SetupProcessRunner
{
    private readonly ILogger<SetupProcessRunner> objLogger;

    /// <summary>
    /// Initialises the runner.
    /// </summary>
    /// <param name="aLogger">Logger for process diagnostics.</param>
    public SetupProcessRunner(ILogger<SetupProcessRunner> aLogger)
    {
        objLogger = aLogger;
    }

    /// <summary>
    /// Runs a process to completion, streaming each output line to an optional callback.
    /// </summary>
    /// <param name="aFileName">The executable path.</param>
    /// <param name="aArguments">The argument list.</param>
    /// <param name="aEnvironment">Extra environment variables (e.g. PYTHONNOUSERSITE=1), or null.</param>
    /// <param name="aOnOutputLine">Callback invoked per stdout/stderr line, or null.</param>
    /// <param name="aCancellationToken">Token killing the process tree when cancelled.</param>
    /// <returns>The process exit code.</returns>
    public async Task<int> RunAsync(
        string aFileName,
        IReadOnlyList<string> aArguments,
        IReadOnlyDictionary<string, string>? aEnvironment,
        Action<string>? aOnOutputLine,
        CancellationToken aCancellationToken)
    {
        var vStartInfo = new ProcessStartInfo(aFileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var vArgument in aArguments)
        {
            vStartInfo.ArgumentList.Add(vArgument);
        }

        if (aEnvironment is not null)
        {
            foreach (var vPair in aEnvironment)
            {
                vStartInfo.Environment[vPair.Key] = vPair.Value;
            }
        }

        using var vProcess = new Process { StartInfo = vStartInfo };
        vProcess.OutputDataReceived += (_, aEvent) => HandleLine(aEvent.Data, aOnOutputLine);
        vProcess.ErrorDataReceived += (_, aEvent) => HandleLine(aEvent.Data, aOnOutputLine);

        vProcess.Start();
        vProcess.BeginOutputReadLine();
        vProcess.BeginErrorReadLine();

        try
        {
            await vProcess.WaitForExitAsync(aCancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(vProcess);
            throw;
        }

        return vProcess.ExitCode;
    }

    private static void HandleLine(string? aLine, Action<string>? aOnOutputLine)
    {
        if (!string.IsNullOrWhiteSpace(aLine))
        {
            aOnOutputLine?.Invoke(aLine);
        }
    }

    private void TryKill(Process aProcess)
    {
        try
        {
            aProcess.Kill(entireProcessTree: true);
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "Failed to kill cancelled setup process");
        }
    }
}
