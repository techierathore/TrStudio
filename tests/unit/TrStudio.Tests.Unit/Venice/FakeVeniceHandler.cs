using System.Net;
using System.Text;

namespace TrStudio.Tests.Unit.Venice;

/// <summary>
/// A fake <see cref="HttpMessageHandler"/> standing in for Venice: answers by request path from a
/// queue of canned responses and records every request (method, address, auth header, body) so a
/// test can prove exactly what was — or was not — sent.
/// </summary>
public sealed class FakeVeniceHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Queue<Func<HttpResponseMessage>>> objRoutes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every request seen, in order.</summary>
    public List<RecordedRequest> Requests { get; } = new();

    /// <summary>Queues a JSON answer for a path suffix (e.g. "image/generate"); the last one repeats.</summary>
    /// <param name="aPath">The path suffix.</param>
    /// <param name="aJson">The body.</param>
    /// <param name="aStatus">The status (default 200).</param>
    /// <returns>This handler.</returns>
    public FakeVeniceHandler Json(string aPath, string aJson, HttpStatusCode aStatus = HttpStatusCode.OK)
        => Add(aPath, () => new HttpResponseMessage(aStatus) { Content = new StringContent(aJson, Encoding.UTF8, "application/json") });

    /// <summary>Queues a binary answer for a path suffix.</summary>
    /// <param name="aPath">The path suffix.</param>
    /// <param name="aBytes">The bytes.</param>
    /// <param name="aMediaType">The content type.</param>
    /// <returns>This handler.</returns>
    public FakeVeniceHandler Binary(string aPath, byte[] aBytes, string aMediaType)
        => Add(aPath, () =>
        {
            var vContent = new ByteArrayContent(aBytes);
            vContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(aMediaType);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = vContent };
        });

    /// <summary>The requests whose path ends with the suffix.</summary>
    /// <param name="aPath">The path suffix.</param>
    /// <returns>The matching requests.</returns>
    public IReadOnlyList<RecordedRequest> To(string aPath)
        => Requests.Where(aRequest => aRequest.Path.EndsWith("/" + aPath, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
    {
        var vBody = aRequest.Content is null ? null : await aRequest.Content.ReadAsStringAsync(aCancellationToken);
        var vPath = aRequest.RequestUri!.AbsolutePath;
        Requests.Add(new RecordedRequest(aRequest.Method.Method, aRequest.RequestUri!, vPath, aRequest.Headers.Authorization?.ToString(), vBody));

        var vKey = objRoutes.Keys.FirstOrDefault(aKey => vPath.EndsWith("/" + aKey, StringComparison.OrdinalIgnoreCase));
        if (vKey is null || objRoutes[vKey].Count == 0)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":\"no fake route\"}", Encoding.UTF8, "application/json") };
        }

        var vQueue = objRoutes[vKey];
        return (vQueue.Count > 1 ? vQueue.Dequeue() : vQueue.Peek())();
    }

    private FakeVeniceHandler Add(string aPath, Func<HttpResponseMessage> aFactory)
    {
        if (!objRoutes.TryGetValue(aPath, out var vQueue))
        {
            vQueue = new Queue<Func<HttpResponseMessage>>();
            objRoutes[aPath] = vQueue;
        }

        vQueue.Enqueue(aFactory);
        return this;
    }

    /// <summary>One recorded request.</summary>
    /// <param name="Method">The HTTP method.</param>
    /// <param name="Uri">The full address.</param>
    /// <param name="Path">The address path.</param>
    /// <param name="Authorization">The Authorization header, if any.</param>
    /// <param name="Body">The body text, if any.</param>
    public sealed record RecordedRequest(string Method, Uri Uri, string Path, string? Authorization, string? Body);
}
