namespace TrStudio.Engine.Domain.Setup;

/// <summary>
/// One guided-setup step (REQ-FN-009). Handlers are IDEMPOTENT: re-running a completed step
/// re-verifies quickly or re-provisions inside the managed root only (REQ-FN-010).
/// </summary>
public interface ISetupStepHandler
{
    /// <summary>The stable step key (one of <see cref="SetupStepKeys"/>).</summary>
    string StepKey { get; }

    /// <summary>
    /// Executes the step, reporting progress as it goes.
    /// </summary>
    /// <param name="aProgress">Progress sink receiving percent + phase message updates.</param>
    /// <param name="aCancellationToken">Token cancelling the step; partial downloads are kept for resume.</param>
    /// <returns>A task completing when the step succeeds.</returns>
    /// <exception cref="SetupStepException">The step failed; the message is actionable for the admin.</exception>
    Task ExecuteAsync(IProgress<SetupStepProgress> aProgress, CancellationToken aCancellationToken);
}
