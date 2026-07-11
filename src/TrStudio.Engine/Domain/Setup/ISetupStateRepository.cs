using TrStudio.Models.Entities;

namespace TrStudio.Engine.Domain.Setup;

/// <summary>
/// Data access for the persisted guided-setup step state ("SetupState" table, script 0300).
/// Instance-global (admin-scoped) — no per-user filter applies to setup state.
/// </summary>
public interface ISetupStateRepository
{
    /// <summary>
    /// Lists all setup step rows in execution order.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>All step rows.</returns>
    Task<IReadOnlyList<SetupStepInfo>> ListAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Updates a step row's state, progress and messages.
    /// </summary>
    /// <param name="aStepKey">The step key ("SetupStateId").</param>
    /// <param name="aState">The new state label (<see cref="SetupStepStates"/>).</param>
    /// <param name="aProgressPercent">Progress 0-100.</param>
    /// <param name="aMessage">Informational phase message, or null to clear.</param>
    /// <param name="aErrorMessage">Actionable error message, or null to clear.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is updated.</returns>
    Task UpdateAsync(string aStepKey, string aState, int aProgressPercent, string? aMessage, string? aErrorMessage, CancellationToken aCancellationToken = default);
}
