using TrStudio.Models.Contracts;

namespace TrStudio.Models.Services;

/// <summary>
/// Admin operations over the RunPod serverless endpoint (REQ-FN-028/BRD-12): status reporting,
/// manual start/stop (scale-to-zero) and the auto idle-stop toggle. Every operation degrades
/// gracefully to a "not configured" status when no RunPod API key is set — it never crashes.
/// </summary>
public interface IRunPodOps
{
    /// <summary>
    /// Reports the current RunPod status (configured flag, worker state, cold-start flag,
    /// idle-stop toggle).
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The status snapshot.</returns>
    Task<RunPodStatusResponse> GetStatusAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Warms the endpoint by scaling it up to at least one worker.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The status after the request.</returns>
    Task<RunPodStatusResponse> StartAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Scales the endpoint to zero workers (idle-stop) to stop incurring cost.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The status after the request.</returns>
    Task<RunPodStatusResponse> StopAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Enables or disables the auto idle-stop (scale-to-zero after an idle job) behaviour.
    /// </summary>
    /// <param name="aEnabled">The new toggle value.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The status after applying the toggle.</returns>
    Task<RunPodStatusResponse> SetAutoIdleStopAsync(bool aEnabled, CancellationToken aCancellationToken = default);
}
