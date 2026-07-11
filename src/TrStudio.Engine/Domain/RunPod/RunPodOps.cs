using Microsoft.Extensions.Logging;
using TrStudio.Models.Contracts;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.RunPod;

/// <summary>
/// Default <see cref="IRunPodOps"/> (REQ-FN-028/BRD-12). Reports RunPod status and drives
/// manual start / scale-to-zero and the auto idle-stop toggle. With no API key configured every
/// operation returns a clear "not configured" status instead of crashing (graceful degrade).
/// </summary>
public class RunPodOps : IRunPodOps
{
    private readonly RunPodOptions objOptions;
    private readonly IRunPodClient objClient;
    private readonly ILogger<RunPodOps> objLogger;

    /// <summary>
    /// Initialises the ops service.
    /// </summary>
    /// <param name="aOptions">Shared RunPod options (the idle-stop toggle is mutated in place).</param>
    /// <param name="aClient">The RunPod REST client seam.</param>
    /// <param name="aLogger">Structured logger.</param>
    public RunPodOps(RunPodOptions aOptions, IRunPodClient aClient, ILogger<RunPodOps> aLogger)
    {
        objOptions = aOptions;
        objClient = aClient;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<RunPodStatusResponse> GetStatusAsync(CancellationToken aCancellationToken = default)
    {
        if (!objOptions.IsConfigured)
        {
            return NotConfigured();
        }

        try
        {
            var vHealth = await objClient.GetHealthAsync(aCancellationToken).ConfigureAwait(false);
            var vWorkerState = vHealth.InProgress > 0 ? "running"
                : vHealth.IsColdStart ? "cold"
                : vHealth.ReadyWorkers > 0 ? "ready" : "idle";

            return new RunPodStatusResponse
            {
                Configured = true,
                WorkerState = vWorkerState,
                ColdStart = vHealth.IsColdStart,
                AutoIdleStop = objOptions.AutoIdleStop,
                EndpointId = objOptions.EndpointId,
                Message = $"RunPod endpoint '{objOptions.EndpointId}' is {vWorkerState}."
            };
        }
        catch (Exception vException) when (vException is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            objLogger.LogWarning(vException, "RunPod health probe failed for endpoint {EndpointId}", objOptions.EndpointId);
            return new RunPodStatusResponse
            {
                Configured = true,
                WorkerState = "unreachable",
                ColdStart = true,
                AutoIdleStop = objOptions.AutoIdleStop,
                EndpointId = objOptions.EndpointId,
                Message = "RunPod is configured but the endpoint health probe failed — check the endpoint id and API key."
            };
        }
    }

    /// <inheritdoc />
    public async Task<RunPodStatusResponse> StartAsync(CancellationToken aCancellationToken = default)
    {
        if (!objOptions.IsConfigured)
        {
            return NotConfigured();
        }

        await objClient.SetMinWorkersAsync(1, aCancellationToken).ConfigureAwait(false);
        objLogger.LogInformation("RunPod endpoint {EndpointId} start requested (min workers = 1)", objOptions.EndpointId);
        return await GetStatusAsync(aCancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<RunPodStatusResponse> StopAsync(CancellationToken aCancellationToken = default)
    {
        if (!objOptions.IsConfigured)
        {
            return NotConfigured();
        }

        await objClient.SetMinWorkersAsync(0, aCancellationToken).ConfigureAwait(false);
        objLogger.LogInformation("RunPod endpoint {EndpointId} scale-to-zero (idle-stop) requested", objOptions.EndpointId);
        return await GetStatusAsync(aCancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<RunPodStatusResponse> SetAutoIdleStopAsync(bool aEnabled, CancellationToken aCancellationToken = default)
    {
        objOptions.AutoIdleStop = aEnabled;
        objLogger.LogInformation("RunPod auto idle-stop toggled to {Enabled}", aEnabled);

        if (!objOptions.IsConfigured)
        {
            var vStatus = NotConfigured();
            vStatus.AutoIdleStop = aEnabled;
            return vStatus;
        }

        return await GetStatusAsync(aCancellationToken).ConfigureAwait(false);
    }

    private RunPodStatusResponse NotConfigured() => new()
    {
        Configured = false,
        WorkerState = "not-configured",
        ColdStart = false,
        AutoIdleStop = objOptions.AutoIdleStop,
        EndpointId = string.IsNullOrWhiteSpace(objOptions.EndpointId) ? null : objOptions.EndpointId,
        Message = RunPodOptions.NotConfiguredMessage
    };
}
