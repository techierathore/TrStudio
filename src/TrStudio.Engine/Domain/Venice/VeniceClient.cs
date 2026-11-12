using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TrStudio.Engine.Domain.Venice;

/// <summary>
/// A small typed client over the named <see cref="HttpClientName"/> <see cref="HttpClient"/>. The
/// base address and <c>Authorization: Bearer</c> key are passed in per call from the database
/// (<see cref="VeniceConnection"/>) — the client itself holds no address and no key (BRD-98). Every
/// non-success answer becomes a <see cref="VeniceApiException"/> carrying Venice's own text.
/// </summary>
public class VeniceClient
{
    /// <summary>The named HttpClient registration.</summary>
    public const string HttpClientName = "Venice";

    private readonly IHttpClientFactory objHttpClientFactory;

    /// <summary>
    /// Initialises the client.
    /// </summary>
    /// <param name="aHttpClientFactory">Factory producing the named Venice HttpClient.</param>
    public VeniceClient(IHttpClientFactory aHttpClientFactory)
    {
        objHttpClientFactory = aHttpClientFactory;
    }

    /// <summary>Lists Venice's public models of one type (<c>GET /models?type=…</c>; no key needed).</summary>
    /// <param name="aBaseUrl">The API base address from the database.</param>
    /// <param name="aType"><c>image</c>, <c>inpaint</c>, <c>upscale</c> or <c>video</c>.</param>
    /// <param name="aApiKey">The key, when one is to be checked (Test connection); null for the public list.</param>
    /// <param name="aCancellationToken">Token to cancel the call.</param>
    /// <returns>The parsed entries.</returns>
    public async Task<IReadOnlyList<VeniceModelInfo>> ListModelsAsync(string aBaseUrl, string aType, string? aApiKey, CancellationToken aCancellationToken)
    {
        using var vRequest = new HttpRequestMessage(HttpMethod.Get, BuildUri(aBaseUrl, $"models?type={Uri.EscapeDataString(aType)}"));
        if (!string.IsNullOrWhiteSpace(aApiKey))
        {
            vRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aApiKey);
        }

        using var vResponse = await SendAsync(vRequest, aCancellationToken).ConfigureAwait(false);
        var vBody = await vResponse.Content.ReadAsStringAsync(aCancellationToken).ConfigureAwait(false);
        return VeniceModelInfo.ParseList(vBody);
    }

    /// <summary>Text to image: <c>POST /image/generate</c>; returns the first picture's bytes.</summary>
    /// <param name="aConnection">Address and key from the database.</param>
    /// <param name="aBody">The JSON body (see <see cref="VeniceRequestBuilder"/>).</param>
    /// <param name="aCancellationToken">Token to cancel the call.</param>
    /// <returns>The decoded picture.</returns>
    public async Task<byte[]> GenerateImageAsync(VeniceConnection aConnection, JsonObject aBody, CancellationToken aCancellationToken)
    {
        using var vResponse = await PostJsonAsync(aConnection, "image/generate", aBody, aCancellationToken).ConfigureAwait(false);
        if (!IsJson(vResponse))
        {
            return await vResponse.Content.ReadAsByteArrayAsync(aCancellationToken).ConfigureAwait(false);
        }

        var vJson = await vResponse.Content.ReadAsStringAsync(aCancellationToken).ConfigureAwait(false);
        using var vDocument = JsonDocument.Parse(vJson);
        if (vDocument.RootElement.TryGetProperty("images", out var vImages)
            && vImages.ValueKind == JsonValueKind.Array
            && vImages.GetArrayLength() > 0
            && vImages[0].GetString() is { Length: > 0 } vFirst)
        {
            return DecodeBase64(vFirst);
        }

        throw new VeniceApiException((int)vResponse.StatusCode, $"{VeniceApiException.Prefix}the answer held no picture (HTTP {(int)vResponse.StatusCode})");
    }

    /// <summary>Image to image: <c>POST /image/edit</c>, or <c>/image/multi-edit</c> for several inputs; returns the picture's bytes.</summary>
    /// <param name="aConnection">Address and key from the database.</param>
    /// <param name="aBody">The JSON body.</param>
    /// <param name="aMulti">True for <c>/image/multi-edit</c>.</param>
    /// <param name="aCancellationToken">Token to cancel the call.</param>
    /// <returns>The edited picture.</returns>
    public async Task<byte[]> EditImageAsync(VeniceConnection aConnection, JsonObject aBody, bool aMulti, CancellationToken aCancellationToken)
    {
        using var vResponse = await PostJsonAsync(aConnection, aMulti ? "image/multi-edit" : "image/edit", aBody, aCancellationToken).ConfigureAwait(false);
        return await vResponse.Content.ReadAsByteArrayAsync(aCancellationToken).ConfigureAwait(false);
    }

    /// <summary>Upscale: <c>POST /image/upscale</c>; returns the PNG bytes.</summary>
    /// <param name="aConnection">Address and key from the database.</param>
    /// <param name="aBody">The JSON body.</param>
    /// <param name="aCancellationToken">Token to cancel the call.</param>
    /// <returns>The upscaled picture.</returns>
    public async Task<byte[]> UpscaleImageAsync(VeniceConnection aConnection, JsonObject aBody, CancellationToken aCancellationToken)
    {
        using var vResponse = await PostJsonAsync(aConnection, "image/upscale", aBody, aCancellationToken).ConfigureAwait(false);
        return await vResponse.Content.ReadAsByteArrayAsync(aCancellationToken).ConfigureAwait(false);
    }

    /// <summary>Video price: <c>POST /video/quote</c>; returns Venice's quote in US dollars.</summary>
    /// <param name="aConnection">Address and key from the database.</param>
    /// <param name="aBody">The JSON body.</param>
    /// <param name="aCancellationToken">Token to cancel the call.</param>
    /// <returns>The quote.</returns>
    public async Task<decimal> QuoteVideoAsync(VeniceConnection aConnection, JsonObject aBody, CancellationToken aCancellationToken)
    {
        using var vResponse = await PostJsonAsync(aConnection, "video/quote", aBody, aCancellationToken).ConfigureAwait(false);
        var vJson = await vResponse.Content.ReadAsStringAsync(aCancellationToken).ConfigureAwait(false);
        using var vDocument = JsonDocument.Parse(vJson);
        return vDocument.RootElement.TryGetProperty("quote", out var vQuote) && vQuote.ValueKind == JsonValueKind.Number
            ? vQuote.GetDecimal()
            : throw new VeniceApiException((int)vResponse.StatusCode, $"{VeniceApiException.Prefix}the quote answer held no price (HTTP {(int)vResponse.StatusCode})");
    }

    /// <summary>Queues a clip: <c>POST /video/queue</c>.</summary>
    /// <param name="aConnection">Address and key from the database.</param>
    /// <param name="aBody">The JSON body.</param>
    /// <param name="aCancellationToken">Token to cancel the call.</param>
    /// <returns>The queue id and, for some models, a download address.</returns>
    public async Task<VeniceQueued> QueueVideoAsync(VeniceConnection aConnection, JsonObject aBody, CancellationToken aCancellationToken)
    {
        using var vResponse = await PostJsonAsync(aConnection, "video/queue", aBody, aCancellationToken).ConfigureAwait(false);
        var vJson = await vResponse.Content.ReadAsStringAsync(aCancellationToken).ConfigureAwait(false);
        using var vDocument = JsonDocument.Parse(vJson);
        var vRoot = vDocument.RootElement;
        var vQueueId = vRoot.TryGetProperty("queue_id", out var vId) ? vId.GetString() : null;
        if (string.IsNullOrWhiteSpace(vQueueId))
        {
            throw new VeniceApiException((int)vResponse.StatusCode, $"{VeniceApiException.Prefix}the queue answer held no queue_id (HTTP {(int)vResponse.StatusCode})");
        }

        var vDownload = vRoot.TryGetProperty("download_url", out var vUrl) && vUrl.ValueKind == JsonValueKind.String ? vUrl.GetString() : null;
        return new VeniceQueued(vQueueId, vDownload);
    }

    /// <summary>Polls a clip: <c>POST /video/retrieve</c>.</summary>
    /// <param name="aConnection">Address and key from the database.</param>
    /// <param name="aModel">The Venice model id.</param>
    /// <param name="aQueueId">The queue id.</param>
    /// <param name="aCancellationToken">Token to cancel the call.</param>
    /// <returns>Still processing (with timings), or done (the mp4 bytes, or a download address).</returns>
    public async Task<VeniceRetrieved> RetrieveVideoAsync(VeniceConnection aConnection, string aModel, string aQueueId, CancellationToken aCancellationToken)
    {
        var vBody = new JsonObject { ["model"] = aModel, ["queue_id"] = aQueueId };
        using var vResponse = await PostJsonAsync(aConnection, "video/retrieve", vBody, aCancellationToken).ConfigureAwait(false);
        if (!IsJson(vResponse))
        {
            var vBytes = await vResponse.Content.ReadAsByteArrayAsync(aCancellationToken).ConfigureAwait(false);
            return new VeniceRetrieved(true, vBytes, null, null, null);
        }

        var vJson = await vResponse.Content.ReadAsStringAsync(aCancellationToken).ConfigureAwait(false);
        using var vDocument = JsonDocument.Parse(vJson);
        var vRoot = vDocument.RootElement;
        var vStatus = vRoot.TryGetProperty("status", out var vS) ? vS.GetString() : null;
        var vDone = string.Equals(vStatus, "COMPLETED", StringComparison.OrdinalIgnoreCase);
        var vDownload = vRoot.TryGetProperty("download_url", out var vUrl) && vUrl.ValueKind == JsonValueKind.String ? vUrl.GetString() : null;
        return new VeniceRetrieved(vDone, null, vDownload, Number(vRoot, "average_execution_time"), Number(vRoot, "execution_duration"));
    }

    /// <summary>Frees Venice's copy of a finished clip: <c>POST /video/complete</c>.</summary>
    /// <param name="aConnection">Address and key from the database.</param>
    /// <param name="aModel">The Venice model id.</param>
    /// <param name="aQueueId">The queue id.</param>
    /// <param name="aCancellationToken">Token to cancel the call.</param>
    /// <returns>A task completing when Venice answers.</returns>
    public async Task CompleteVideoAsync(VeniceConnection aConnection, string aModel, string aQueueId, CancellationToken aCancellationToken)
    {
        var vBody = new JsonObject { ["model"] = aModel, ["queue_id"] = aQueueId };
        using var vResponse = await PostJsonAsync(aConnection, "video/complete", vBody, aCancellationToken).ConfigureAwait(false);
    }

    /// <summary>Downloads a finished clip from Venice's pre-signed address (no key sent).</summary>
    /// <param name="aUrl">The <c>download_url</c>.</param>
    /// <param name="aCancellationToken">Token to cancel the call.</param>
    /// <returns>The mp4 bytes.</returns>
    public async Task<byte[]> DownloadAsync(string aUrl, CancellationToken aCancellationToken)
    {
        using var vRequest = new HttpRequestMessage(HttpMethod.Get, aUrl);
        using var vResponse = await SendAsync(vRequest, aCancellationToken).ConfigureAwait(false);
        return await vResponse.Content.ReadAsByteArrayAsync(aCancellationToken).ConfigureAwait(false);
    }

    /// <summary>Joins the database-held base address and a relative path.</summary>
    /// <param name="aBaseUrl">The base address (with or without a trailing slash).</param>
    /// <param name="aPath">The relative path.</param>
    /// <returns>The absolute address.</returns>
    public static Uri BuildUri(string aBaseUrl, string aPath)
        => new(aBaseUrl.TrimEnd('/') + "/" + aPath.TrimStart('/'), UriKind.Absolute);

    private async Task<HttpResponseMessage> PostJsonAsync(VeniceConnection aConnection, string aPath, JsonObject aBody, CancellationToken aCancellationToken)
    {
        using var vRequest = new HttpRequestMessage(HttpMethod.Post, BuildUri(aConnection.BaseUrl, aPath))
        {
            Content = new StringContent(aBody.ToJsonString(), Encoding.UTF8, "application/json")
        };
        vRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aConnection.ApiKey);
        return await SendAsync(vRequest, aCancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
    {
        var vClient = objHttpClientFactory.CreateClient(HttpClientName);
        HttpResponseMessage vResponse;
        try
        {
            vResponse = await vClient.SendAsync(aRequest, aCancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException vException)
        {
            throw new VeniceApiException(null, $"{VeniceApiException.Prefix}{vException.Message}");
        }

        if (vResponse.IsSuccessStatusCode)
        {
            return vResponse;
        }

        using (vResponse)
        {
            var vBody = await vResponse.Content.ReadAsStringAsync(aCancellationToken).ConfigureAwait(false);
            throw VeniceApiException.FromResponse(vResponse.StatusCode, vBody, vResponse.Headers);
        }
    }

    private static bool IsJson(HttpResponseMessage aResponse)
        => aResponse.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true;

    private static double? Number(JsonElement aRoot, string aKey)
        => aRoot.TryGetProperty(aKey, out var vValue) && vValue.ValueKind == JsonValueKind.Number ? vValue.GetDouble() : null;

    private static byte[] DecodeBase64(string aValue)
    {
        var vComma = aValue.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? aValue.IndexOf(',') : -1;
        return Convert.FromBase64String(vComma >= 0 ? aValue[(vComma + 1)..] : aValue);
    }
}

/// <summary>A queued Venice clip.</summary>
/// <param name="QueueId">Venice's queue id.</param>
/// <param name="DownloadUrl">The pre-signed download address, for models that give one.</param>
public sealed record VeniceQueued(string QueueId, string? DownloadUrl);

/// <summary>One <c>/video/retrieve</c> answer.</summary>
/// <param name="Done">Whether the clip is finished.</param>
/// <param name="Video">The mp4 bytes when Venice returned them directly.</param>
/// <param name="DownloadUrl">The download address when Venice gave one instead.</param>
/// <param name="AverageMs">Venice's P80 execution time estimate in milliseconds.</param>
/// <param name="ElapsedMs">How long the clip has been running, in milliseconds.</param>
public sealed record VeniceRetrieved(bool Done, byte[]? Video, string? DownloadUrl, double? AverageMs, double? ElapsedMs)
{
    /// <summary>Progress 1..95 from Venice's timings (never 100 until the clip is stored).</summary>
    public int Progress => AverageMs is > 0 && ElapsedMs is >= 0
        ? (int)Math.Clamp(ElapsedMs.Value / AverageMs.Value * 100d, 1d, 95d)
        : 1;

    /// <summary>Time left from Venice's timings, when stated.</summary>
    public TimeSpan? Eta => AverageMs is > 0 && ElapsedMs is >= 0
        ? TimeSpan.FromMilliseconds(Math.Max(0d, AverageMs.Value - ElapsedMs.Value))
        : null;
}
