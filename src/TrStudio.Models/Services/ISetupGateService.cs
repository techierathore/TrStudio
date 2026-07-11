using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;

namespace TrStudio.Models.Services;

/// <summary>
/// Evaluates the setup gate: whether the instance has completed first-run setup, has an
/// enabled model source and a ready model, and where the UI should route the user.
/// </summary>
public interface ISetupGateService
{
    /// <summary>
    /// Evaluates the setup gate for a user.
    /// </summary>
    /// <param name="aUserId">The AppManager user id of the requesting user.</param>
    /// <param name="aRole">The user's application role (owners/operators may be routed to setup).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The gate verdict and redirect path.</returns>
    Task<SetupGateResponse> EvaluateAsync(string aUserId, UserRole aRole, CancellationToken aCancellationToken = default);
}
