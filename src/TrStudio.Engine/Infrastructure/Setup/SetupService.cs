using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Setup;
using TrStudio.Models.Entities;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Infrastructure.Setup;

/// <summary>
/// The guided-setup step engine (REQ-FN-009): implements <see cref="ISetupService"/> and
/// <see cref="ISetupStepEngine"/>. One step runs at a time; state and progress are persisted
/// to the "SetupState" table so the wizard is pollable and survives restarts. Failures are
/// persisted as ACTIONABLE messages (<see cref="SetupStepException"/>), never stack traces;
/// cancellation keeps partial downloads so a re-run resumes.
/// </summary>
public class SetupService : ISetupService, ISetupStepEngine
{
    private readonly ISetupStateRepository objStateRepository;
    private readonly IReadOnlyDictionary<string, ISetupStepHandler> objHandlers;
    private readonly ILogger<SetupService> objLogger;
    private readonly object objSync = new();

    private CancellationTokenSource? objCurrentCts;
    private string? objCurrentStepKey;
    private Task? objCurrentRun;
    private int objCurrentPercent;

    /// <summary>
    /// Initialises the engine from the registered step handlers.
    /// </summary>
    /// <param name="aStateRepository">Persisted step-state repository.</param>
    /// <param name="aHandlers">The registered step handlers (one per <see cref="SetupStepKeys"/> key).</param>
    /// <param name="aLogger">Logger for step diagnostics (full exceptions are logged here).</param>
    public SetupService(ISetupStateRepository aStateRepository, IEnumerable<ISetupStepHandler> aHandlers, ILogger<SetupService> aLogger)
    {
        objStateRepository = aStateRepository;
        objHandlers = aHandlers.ToDictionary(aHandler => aHandler.StepKey, StringComparer.OrdinalIgnoreCase);
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<SetupStatus> GetStatusAsync(CancellationToken aCancellationToken = default)
    {
        var vSteps = await objStateRepository.ListAsync(aCancellationToken).ConfigureAwait(false);
        var vRunning = vSteps.FirstOrDefault(aStep => aStep.State == SetupStepStates.Running);
        var vFailed = vSteps.FirstOrDefault(aStep => aStep.State == SetupStepStates.Failed);
        return new SetupStatus
        {
            OnnxInstalled = IsCompleted(vSteps, SetupStepKeys.OnnxRuntime),
            PythonEnvReady = IsCompleted(vSteps, SetupStepKeys.PythonEnv),
            ComfyUiReady = IsCompleted(vSteps, SetupStepKeys.ComfyUi),
            StepsCompleted = vSteps.Where(aStep => aStep.State == SetupStepStates.Completed).Select(aStep => aStep.StepName).ToArray(),
            CurrentStep = vRunning?.StepName,
            ErrorMessage = vFailed?.ErrorMessage,
            ProgressPercent = vRunning?.ProgressPercent ?? 0,
            Steps = vSteps.ToArray()
        };
    }

    /// <inheritdoc />
    public async Task InstallOnnxAsync(CancellationToken aCancellationToken = default)
    {
        await RunStepAndWaitAsync(SetupStepKeys.OnnxRuntime, aCancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ProvisionPythonEnvAsync(CancellationToken aCancellationToken = default)
    {
        await RunStepAndWaitAsync(SetupStepKeys.PythonEnv, aCancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> StartStepAsync(string aStepKey, CancellationToken aCancellationToken = default)
    {
        var vHandler = ResolveHandler(aStepKey);
        CancellationTokenSource vCts;
        lock (objSync)
        {
            if (objCurrentStepKey is not null)
            {
                return false;
            }

            vCts = new CancellationTokenSource();
            objCurrentCts = vCts;
            objCurrentStepKey = vHandler.StepKey;
            objCurrentPercent = 0;
        }

        await objStateRepository.UpdateAsync(vHandler.StepKey, SetupStepStates.Running, 0, "Starting", null, aCancellationToken).ConfigureAwait(false);
        lock (objSync)
        {
            objCurrentRun = Task.Run(() => ExecuteTrackedAsync(vHandler, vCts.Token), CancellationToken.None);
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> StartAllAsync(CancellationToken aCancellationToken = default)
    {
        CancellationTokenSource vCts;
        lock (objSync)
        {
            if (objCurrentStepKey is not null)
            {
                return false;
            }

            vCts = new CancellationTokenSource();
            objCurrentCts = vCts;
            objCurrentStepKey = "*";
            objCurrentPercent = 0;
        }

        lock (objSync)
        {
            objCurrentRun = Task.Run(() => ExecuteAllTrackedAsync(vCts.Token), CancellationToken.None);
        }

        await Task.CompletedTask.ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public Task<bool> CancelAsync(CancellationToken aCancellationToken = default)
    {
        lock (objSync)
        {
            if (objCurrentCts is null || objCurrentStepKey is null)
            {
                return Task.FromResult(false);
            }

            objCurrentCts.Cancel();
            return Task.FromResult(true);
        }
    }

    private async Task RunStepAndWaitAsync(string aStepKey, CancellationToken aCancellationToken)
    {
        var vStarted = await StartStepAsync(aStepKey, aCancellationToken).ConfigureAwait(false);
        if (!vStarted)
        {
            throw new InvalidOperationException("Another setup step is already running — wait for it to finish or cancel it first.");
        }

        Task? vRun;
        lock (objSync)
        {
            vRun = objCurrentRun;
        }

        if (vRun is not null)
        {
            await vRun.ConfigureAwait(false);
        }
    }

    private async Task ExecuteAllTrackedAsync(CancellationToken aCancellationToken)
    {
        try
        {
            foreach (var vStepKey in SetupStepKeys.Ordered)
            {
                var vSteps = await objStateRepository.ListAsync(CancellationToken.None).ConfigureAwait(false);
                if (IsCompleted(vSteps, vStepKey))
                {
                    continue;
                }

                var vHandler = ResolveHandler(vStepKey);
                lock (objSync)
                {
                    objCurrentStepKey = vStepKey;
                    objCurrentPercent = 0;
                }

                await objStateRepository.UpdateAsync(vStepKey, SetupStepStates.Running, 0, "Starting", null, CancellationToken.None).ConfigureAwait(false);
                var vSucceeded = await ExecuteStepCoreAsync(vHandler, aCancellationToken).ConfigureAwait(false);
                if (!vSucceeded)
                {
                    return;
                }
            }
        }
        finally
        {
            ClearCurrentRun();
        }
    }

    private async Task ExecuteTrackedAsync(ISetupStepHandler aHandler, CancellationToken aCancellationToken)
    {
        try
        {
            await ExecuteStepCoreAsync(aHandler, aCancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ClearCurrentRun();
        }
    }

    private async Task<bool> ExecuteStepCoreAsync(ISetupStepHandler aHandler, CancellationToken aCancellationToken)
    {
        var vProgressSink = new SetupProgressPersister(this, aHandler.StepKey);
        try
        {
            await aHandler.ExecuteAsync(vProgressSink, aCancellationToken).ConfigureAwait(false);
            await objStateRepository.UpdateAsync(aHandler.StepKey, SetupStepStates.Completed, 100, vProgressSink.LastMessage ?? "Completed", null, CancellationToken.None).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            objLogger.LogInformation("Setup step {StepKey} cancelled at {Percent}%", aHandler.StepKey, objCurrentPercent);
            await objStateRepository.UpdateAsync(
                aHandler.StepKey, SetupStepStates.Cancelled, objCurrentPercent,
                "Cancelled — partial downloads are kept; run the step again to resume.", null, CancellationToken.None).ConfigureAwait(false);
            return false;
        }
        catch (SetupStepException vStepException)
        {
            objLogger.LogError(vStepException, "Setup step {StepKey} failed", aHandler.StepKey);
            await objStateRepository.UpdateAsync(
                aHandler.StepKey, SetupStepStates.Failed, objCurrentPercent, null, vStepException.Message, CancellationToken.None).ConfigureAwait(false);
            return false;
        }
        catch (Exception vException)
        {
            objLogger.LogError(vException, "Setup step {StepKey} failed unexpectedly", aHandler.StepKey);
            var vActionable = $"The step failed unexpectedly: {vException.Message}. " +
                              "Check the TrStudioApi logs for details and re-run the step — partial progress is kept.";
            await objStateRepository.UpdateAsync(
                aHandler.StepKey, SetupStepStates.Failed, objCurrentPercent, null, vActionable, CancellationToken.None).ConfigureAwait(false);
            return false;
        }
    }

    private void PersistProgress(string aStepKey, SetupStepProgress aProgress)
    {
        lock (objSync)
        {
            objCurrentPercent = aProgress.Percent;
        }

        try
        {
            objStateRepository.UpdateAsync(aStepKey, SetupStepStates.Running, aProgress.Percent, aProgress.Message, null, CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "Persisting setup progress for {StepKey} failed", aStepKey);
        }
    }

    private ISetupStepHandler ResolveHandler(string aStepKey)
    {
        if (!objHandlers.TryGetValue(aStepKey, out var vHandler))
        {
            throw new ArgumentException($"Unknown setup step '{aStepKey}'. Valid steps: {string.Join(", ", objHandlers.Keys)}.", nameof(aStepKey));
        }

        return vHandler;
    }

    private void ClearCurrentRun()
    {
        lock (objSync)
        {
            objCurrentCts?.Dispose();
            objCurrentCts = null;
            objCurrentStepKey = null;
            objCurrentRun = null;
        }
    }

    private static bool IsCompleted(IReadOnlyList<SetupStepInfo> aSteps, string aStepKey)
        => aSteps.Any(aStep => aStep.StepKey == aStepKey && aStep.State == SetupStepStates.Completed);

    /// <summary>
    /// Throttled progress sink persisting step progress to the "SetupState" row (max ~1 write/sec,
    /// but the latest message is always kept for the final state write).
    /// </summary>
    private sealed class SetupProgressPersister : IProgress<SetupStepProgress>
    {
        private readonly SetupService objOwner;
        private readonly string objStepKey;
        private DateTime objLastWriteUtc = DateTime.MinValue;

        public SetupProgressPersister(SetupService aOwner, string aStepKey)
        {
            objOwner = aOwner;
            objStepKey = aStepKey;
        }

        public string? LastMessage { get; private set; }

        public void Report(SetupStepProgress aValue)
        {
            LastMessage = aValue.Message;
            var vNowUtc = DateTime.UtcNow;
            if (aValue.Percent < 100 && (vNowUtc - objLastWriteUtc).TotalMilliseconds < 750)
            {
                return;
            }

            objLastWriteUtc = vNowUtc;
            objOwner.PersistProgress(objStepKey, aValue);
        }
    }
}
