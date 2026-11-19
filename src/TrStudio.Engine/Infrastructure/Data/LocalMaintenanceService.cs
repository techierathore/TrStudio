using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Infrastructure.Setup;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// At start the app loads the kept ComfyUI address (ADR-029) and removes the log rows past their keep time
/// (ADR-030); then it does the removal once a day. A failure here is logged and never stops the app.
/// </summary>
public sealed class LocalMaintenanceService : IHostedService, IDisposable
{
    private readonly ComfyUiConnector objConnector;
    private readonly LogRetentionService objRetention;
    private readonly ILogger<LocalMaintenanceService> objLogger;
    private Timer? objTimer;

    /// <summary>Creates the service.</summary>
    /// <param name="aConnector">The ComfyUI connector.</param>
    /// <param name="aRetention">The log-retention service.</param>
    /// <param name="aLogger">Logger.</param>
    public LocalMaintenanceService(ComfyUiConnector aConnector, LogRetentionService aRetention, ILogger<LocalMaintenanceService> aLogger)
    {
        objConnector = aConnector;
        objRetention = aRetention;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken aCancellationToken)
    {
        try
        {
            await objConnector.LoadSavedAsync(aCancellationToken).ConfigureAwait(false);
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "The kept ComfyUI address could not be loaded");
        }

        await PurgeAsync().ConfigureAwait(false);
        objTimer = new Timer(_ => _ = PurgeAsync(), null, TimeSpan.FromDays(1), TimeSpan.FromDays(1));
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken aCancellationToken)
    {
        objTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose() => objTimer?.Dispose();

    private async Task PurgeAsync()
    {
        try
        {
            await objRetention.PurgeSavedAsync().ConfigureAwait(false);
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "Removing old log rows failed");
        }
    }
}
