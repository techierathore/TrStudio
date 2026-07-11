using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TrStudio.Engine.Domain.RunPod;

/// <summary>
/// HTTP <see cref="IRunPodClient"/> over the RunPod serverless REST API (REQ-FN-028). Structural
/// implementation: it builds and issues the documented requests but is exercised live only once a
/// real <c>RunPod:ApiKey</c> is supplied (live cold-start is deferred). The API key is sent as a
/// bearer header and is never logged.
/// </summary>
public class RunPodHttpClient : IRunPodClient
{
    /// <summary>The named <see cref="HttpClient"/> registration for RunPod requests.</summary>
    public const string HttpClientName = "RunPod";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClientFactory objHttpClientFactory;
    private readonly RunPodOptions objOptions;
    private readonly ILogger<RunPodHttpClient> objLogger;

    /// <summary>
    /// Initialises the client.
    /// </summary>
    /// <param name="aHttpClientFactory">Factory producing the named RunPod HTTP client.</param>
    /// <param name="aOptions">RunPod options carrying the base URL, endpoint id and API key.</param>
    /// <param name="aLogger">Structured logger (never logs the API key).</param>
    public RunPodHttpClient(IHttpClientFactory aHttpClientFactory, RunPodOptions aOptions, ILogger<RunPodHttpClient> aLogger)
    {
        objHttpClientFactory = aHttpClientFactory;
        objOptions = aOptions;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<string> SubmitAsync(string aInputJson, CancellationToken aCancellationToken)
    {
        using var vClient = CreateClient();
        using var vContent = new StringContent($"{{\"input\":{NormaliseInput(aInputJson)}}}", Encoding.UTF8, "application/json");
        using var vResponse = await vClient.PostAsync($"{BaseEndpoint()}/run", vContent, aCancellationToken).ConfigureAwait(false);
        vResponse.EnsureSuccessStatusCode();

        var vBody = await vResponse.Content.ReadFromJsonAsync<RunPodRunEnvelope>(JsonOptions, aCancellationToken).ConfigureAwait(false);
        return vBody?.Id ?? throw new InvalidOperationException("RunPod /run did not return a run id.");
    }

    /// <inheritdoc />
    public async Task<RunPodRunStatus> GetRunStatusAsync(string aRunId, CancellationToken aCancellationToken)
    {
        using var vClient = CreateClient();
        using var vResponse = await vClient.GetAsync($"{BaseEndpoint()}/status/{aRunId}", aCancellationToken).ConfigureAwait(false);
        vResponse.EnsureSuccessStatusCode();

        var vBody = await vResponse.Content.ReadFromJsonAsync<RunPodRunEnvelope>(JsonOptions, aCancellationToken).ConfigureAwait(false);
        var vStatus = vBody?.Status ?? "FAILED";
        var vProgress = string.Equals(vStatus, "COMPLETED", StringComparison.OrdinalIgnoreCase) ? 100
            : string.Equals(vStatus, "IN_PROGRESS", StringComparison.OrdinalIgnoreCase) ? 50 : 0;
        var vOutput = vBody?.Output.HasValue == true ? vBody.Output.Value.GetRawText() : null;
        return new RunPodRunStatus(vStatus, vProgress, vOutput, vBody?.Error);
    }

    /// <inheritdoc />
    public async Task<RunPodHealth> GetHealthAsync(CancellationToken aCancellationToken)
    {
        using var vClient = CreateClient();
        using var vResponse = await vClient.GetAsync($"{BaseEndpoint()}/health", aCancellationToken).ConfigureAwait(false);
        vResponse.EnsureSuccessStatusCode();

        var vBody = await vResponse.Content.ReadFromJsonAsync<RunPodHealthEnvelope>(JsonOptions, aCancellationToken).ConfigureAwait(false);
        var vReady = vBody?.Workers?.Ready ?? 0;
        var vIdle = vBody?.Workers?.Idle ?? 0;
        var vInProgress = vBody?.Jobs?.InProgress ?? 0;
        return new RunPodHealth(vReady, vIdle, vInProgress, vReady == 0 && vIdle == 0);
    }

    /// <inheritdoc />
    public async Task SetMinWorkersAsync(int aMinWorkers, CancellationToken aCancellationToken)
    {
        // RunPod exposes worker-count control via its GraphQL/REST management surface; the exact
        // shape is applied here structurally and verified live once a key is supplied.
        using var vClient = CreateClient();
        using var vContent = new StringContent(
            $"{{\"workersMin\":{Math.Max(0, aMinWorkers)}}}", Encoding.UTF8, "application/json");
        using var vResponse = await vClient.PostAsync($"{BaseEndpoint()}/workers", vContent, aCancellationToken).ConfigureAwait(false);
        if (!vResponse.IsSuccessStatusCode)
        {
            objLogger.LogWarning(
                "RunPod worker-scale request to min {MinWorkers} returned {StatusCode}",
                aMinWorkers, (int)vResponse.StatusCode);
        }
    }

    private HttpClient CreateClient()
    {
        var vClient = objHttpClientFactory.CreateClient(HttpClientName);
        vClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", objOptions.ApiKey);
        return vClient;
    }

    private string BaseEndpoint() => $"{objOptions.BaseUrl.TrimEnd('/')}/{objOptions.EndpointId}";

    private static string NormaliseInput(string aInputJson)
        => string.IsNullOrWhiteSpace(aInputJson) ? "{}" : aInputJson;

    private sealed class RunPodRunEnvelope
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
        public JsonElement? Output { get; set; }
        public string? Error { get; set; }
    }

    private sealed class RunPodHealthEnvelope
    {
        public WorkersBlock? Workers { get; set; }
        public JobsBlock? Jobs { get; set; }

        public sealed class WorkersBlock
        {
            public int Ready { get; set; }
            public int Idle { get; set; }
        }

        public sealed class JobsBlock
        {
            public int InProgress { get; set; }
        }
    }
}
