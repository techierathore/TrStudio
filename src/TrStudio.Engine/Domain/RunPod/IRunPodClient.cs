namespace TrStudio.Engine.Domain.RunPod;

/// <summary>
/// The RunPod serverless REST seam (REQ-FN-028). Abstracts submit/status/health/scale so the
/// provider and ops service can be unit-tested against a faked client with no live endpoint —
/// live cold-start is exercised only once a real RunPod key is supplied.
/// </summary>
public interface IRunPodClient
{
    /// <summary>
    /// Submits a job payload to the serverless endpoint (<c>/run</c>) and returns the run id.
    /// </summary>
    /// <param name="aInputJson">The provider-specific input payload as a JSON string.</param>
    /// <param name="aCancellationToken">Token to cancel the submission.</param>
    /// <returns>The RunPod run id used to poll status.</returns>
    Task<string> SubmitAsync(string aInputJson, CancellationToken aCancellationToken);

    /// <summary>
    /// Polls a submitted run's status (<c>/status/{id}</c>).
    /// </summary>
    /// <param name="aRunId">The run id returned by <see cref="SubmitAsync"/>.</param>
    /// <param name="aCancellationToken">Token to cancel the poll.</param>
    /// <returns>The current run status snapshot.</returns>
    Task<RunPodRunStatus> GetRunStatusAsync(string aRunId, CancellationToken aCancellationToken);

    /// <summary>
    /// Reads the endpoint's worker health (<c>/health</c>): ready/idle worker counts and whether
    /// it has scaled to zero (cold-start pending).
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the request.</param>
    /// <returns>The endpoint health snapshot.</returns>
    Task<RunPodHealth> GetHealthAsync(CancellationToken aCancellationToken);

    /// <summary>
    /// Sets the endpoint's minimum worker count — <c>1</c> to warm/start, <c>0</c> to
    /// scale-to-zero (idle-stop).
    /// </summary>
    /// <param name="aMinWorkers">The desired minimum worker count.</param>
    /// <param name="aCancellationToken">Token to cancel the request.</param>
    /// <returns>A task completing when the scale request is accepted.</returns>
    Task SetMinWorkersAsync(int aMinWorkers, CancellationToken aCancellationToken);
}

/// <summary>
/// A RunPod run status snapshot.
/// </summary>
/// <param name="Status">RunPod status token: <c>IN_QUEUE</c>, <c>IN_PROGRESS</c>, <c>COMPLETED</c>, <c>FAILED</c> or <c>CANCELLED</c>.</param>
/// <param name="Progress">Best-effort progress percentage from 0 to 100.</param>
/// <param name="OutputJson">The run output payload as a JSON string when completed.</param>
/// <param name="Error">The error detail when the run failed.</param>
public record RunPodRunStatus(string Status, int Progress, string? OutputJson, string? Error);

/// <summary>
/// A RunPod endpoint health snapshot.
/// </summary>
/// <param name="ReadyWorkers">Workers ready to accept jobs.</param>
/// <param name="IdleWorkers">Workers idle (running but not processing).</param>
/// <param name="InProgress">Jobs currently in progress.</param>
/// <param name="IsColdStart">Whether the endpoint has scaled to zero and the next job pays a cold-start penalty.</param>
public record RunPodHealth(int ReadyWorkers, int IdleWorkers, int InProgress, bool IsColdStart);
