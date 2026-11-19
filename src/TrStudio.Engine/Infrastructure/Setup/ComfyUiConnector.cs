using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Comfy;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Models.Contracts;

namespace TrStudio.Engine.Infrastructure.Setup;

/// <summary>
/// REQ-FN-009 / REQ-FN-010 (ADR-029): the Mac guided-setup ComfyUI step. It asks a ComfyUI that already
/// runs for its status (<c>GET {address}/system_stats</c>), and on success keeps the address in the app
/// database and makes the running app use it. It starts no program and downloads nothing.
/// </summary>
public class ComfyUiConnector
{
    /// <summary>Name of the HTTP client used for the check.</summary>
    public const string HttpClientName = "ComfyUiProbe";

    /// <summary>How long a check waits for an answer.</summary>
    public static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory objHttpClientFactory;
    private readonly IAppSettingStore objSettings;
    private readonly ComfyUiOptions objOptions;
    private readonly ILogger<ComfyUiConnector> objLogger;

    /// <summary>Creates the connector.</summary>
    /// <param name="aHttpClientFactory">Factory of the HTTP client for the check.</param>
    /// <param name="aSettings">The app's key/value settings.</param>
    /// <param name="aOptions">The shared ComfyUI options (the running app reads its base URL).</param>
    /// <param name="aLogger">Logger.</param>
    public ComfyUiConnector(IHttpClientFactory aHttpClientFactory, IAppSettingStore aSettings, ComfyUiOptions aOptions, ILogger<ComfyUiConnector> aLogger)
    {
        objHttpClientFactory = aHttpClientFactory;
        objSettings = aSettings;
        objOptions = aOptions;
        objLogger = aLogger;
    }

    /// <summary>Checks the address; on success keeps it and uses it at once. A failure keeps nothing.</summary>
    /// <param name="aAddress">The address typed by the Operator.</param>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>The result with a plain message.</returns>
    public async Task<ComfyUiConnectResult> ConnectAsync(string? aAddress, CancellationToken aCancellationToken = default)
    {
        var vResult = await CheckAsync(aAddress, aCancellationToken).ConfigureAwait(false);
        if (vResult.Connected)
        {
            await objSettings.SetAsync(AppSettingStore.ComfyUiBaseUrlKey, vResult.Address, aCancellationToken).ConfigureAwait(false);
            objOptions.BaseUrl = vResult.Address;
        }

        return vResult;
    }

    /// <summary>Checks the saved address (if any) without changing anything.</summary>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>The result; <c>Address</c> is empty when nothing was saved.</returns>
    public async Task<ComfyUiConnectResult> CheckSavedAsync(CancellationToken aCancellationToken = default)
    {
        var vSaved = await objSettings.GetAsync(AppSettingStore.ComfyUiBaseUrlKey, aCancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(vSaved))
        {
            vSaved = objOptions.BaseUrl;
        }

        if (string.IsNullOrWhiteSpace(vSaved))
        {
            return new ComfyUiConnectResult { Message = "No ComfyUI address is kept yet. Enter the address of a running ComfyUI and press Connect." };
        }

        return await CheckAsync(vSaved, aCancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads the kept address into the shared options (at start).</summary>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task LoadSavedAsync(CancellationToken aCancellationToken = default)
    {
        var vSaved = await objSettings.GetAsync(AppSettingStore.ComfyUiBaseUrlKey, aCancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(vSaved))
        {
            objOptions.BaseUrl = vSaved;
        }
    }

    private async Task<ComfyUiConnectResult> CheckAsync(string? aAddress, CancellationToken aCancellationToken)
    {
        var vAddress = (aAddress ?? string.Empty).Trim().TrimEnd('/');
        if (!Uri.TryCreate(vAddress, UriKind.Absolute, out var vUri) || (vUri.Scheme != Uri.UriSchemeHttp && vUri.Scheme != Uri.UriSchemeHttps))
        {
            return new ComfyUiConnectResult
            {
                Address = vAddress,
                Message = "That is not an address. Write it like http://127.0.0.1:8188, then try again."
            };
        }

        var vNothing = $"Nothing answered at {vAddress} — start ComfyUI, then try again.";
        try
        {
            using var vTimeout = CancellationTokenSource.CreateLinkedTokenSource(aCancellationToken);
            vTimeout.CancelAfter(CheckTimeout);
            var vClient = objHttpClientFactory.CreateClient(HttpClientName);
            using var vResponse = await vClient.GetAsync(vAddress + "/system_stats", vTimeout.Token).ConfigureAwait(false);
            if (!vResponse.IsSuccessStatusCode)
            {
                return new ComfyUiConnectResult { Address = vAddress, Message = $"{vAddress} answered, but not as ComfyUI does (status {(int)vResponse.StatusCode}). Check the address, then try again." };
            }

            var vBody = await vResponse.Content.ReadAsStringAsync(vTimeout.Token).ConfigureAwait(false);
            using var vDocument = JsonDocument.Parse(vBody);
            if (vDocument.RootElement.ValueKind != JsonValueKind.Object || !vDocument.RootElement.TryGetProperty("system", out var vSystem) || vSystem.ValueKind != JsonValueKind.Object)
            {
                return new ComfyUiConnectResult { Address = vAddress, Message = $"Something answered at {vAddress}, but it is not ComfyUI. Check the address, then try again." };
            }

            var vVersion = vSystem.TryGetProperty("comfyui_version", out var vVersionElement) && vVersionElement.ValueKind == JsonValueKind.String
                ? vVersionElement.GetString()
                : null;
            return new ComfyUiConnectResult
            {
                Connected = true,
                Address = vAddress,
                Version = vVersion,
                Message = vVersion is null ? $"Connected to ComfyUI at {vAddress}." : $"Connected to ComfyUI {vVersion} at {vAddress}."
            };
        }
        catch (JsonException)
        {
            return new ComfyUiConnectResult { Address = vAddress, Message = $"Something answered at {vAddress}, but it is not ComfyUI. Check the address, then try again." };
        }
        catch (Exception vException) when (vException is HttpRequestException or OperationCanceledException or InvalidOperationException)
        {
            if (aCancellationToken.IsCancellationRequested)
            {
                throw;
            }

            objLogger.LogInformation("ComfyUI check at {Address} failed: {Reason}", vAddress, vException.GetType().Name);
            return new ComfyUiConnectResult { Address = vAddress, Message = vNothing };
        }
    }
}
