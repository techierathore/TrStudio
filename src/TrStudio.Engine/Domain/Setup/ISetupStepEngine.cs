using TrStudio.Models.Entities;

namespace TrStudio.Engine.Domain.Setup;

/// <summary>
/// The guided-setup step engine driving the admin setup endpoints: starts steps in the
/// background (progress is persisted and pollable via <see cref="GetStatusAsync"/>), runs the
/// whole sequence, and cancels the in-flight step leaving resumable state.
/// </summary>
public interface ISetupStepEngine
{
    /// <summary>
    /// Gets the current setup snapshot including per-step detail rows.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The current <see cref="SetupStatus"/>.</returns>
    Task<SetupStatus> GetStatusAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Starts one step in the background and returns immediately; poll <see cref="GetStatusAsync"/> for progress.
    /// </summary>
    /// <param name="aStepKey">The step key (one of <see cref="SetupStepKeys"/>).</param>
    /// <param name="aCancellationToken">Token to cancel starting (not the step itself).</param>
    /// <returns>True when the step was started; false when a step is already running.</returns>
    /// <exception cref="ArgumentException">The step key is unknown.</exception>
    Task<bool> StartStepAsync(string aStepKey, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Starts all incomplete steps in order in the background, stopping at the first failure.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel starting (not the run itself).</param>
    /// <returns>True when the run was started; false when a step is already running.</returns>
    Task<bool> StartAllAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Cancels the currently running step, if any. Partial downloads are kept so a re-run resumes.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>True when a running step was cancelled; false when nothing was running.</returns>
    Task<bool> CancelAsync(CancellationToken aCancellationToken = default);
}
