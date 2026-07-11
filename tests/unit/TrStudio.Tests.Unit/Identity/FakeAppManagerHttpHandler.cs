using System.Net;
using System.Text;

namespace TrStudio.Tests.Unit.Identity;

/// <summary>
/// Test double for <see cref="HttpMessageHandler"/>: routes requests to canned JSON responses
/// by path suffix and records every request (URL and body) for assertions.
/// </summary>
public sealed class FakeAppManagerHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, string Json)> objResponses = new();

    /// <summary>The requests received, as (path, body) pairs in call order.</summary>
    public List<(string Path, string? Body)> Requests { get; } = new();

    /// <summary>
    /// Registers a canned JSON response for any request whose path ends with the given suffix.
    /// </summary>
    /// <param name="aPathSuffix">The path suffix to match (e.g. "/AuthSvc/login").</param>
    /// <param name="aStatus">The HTTP status code to return.</param>
    /// <param name="aJson">The JSON body to return.</param>
    public void Respond(string aPathSuffix, HttpStatusCode aStatus, string aJson)
    {
        objResponses[aPathSuffix] = (aStatus, aJson);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
    {
        var vPath = aRequest.RequestUri!.AbsolutePath;
        var vBody = aRequest.Content == null ? null : await aRequest.Content.ReadAsStringAsync(aCancellationToken);
        Requests.Add((vPath, vBody));

        var vMatch = objResponses.FirstOrDefault(aEntry => vPath.EndsWith(aEntry.Key, StringComparison.OrdinalIgnoreCase));
        if (vMatch.Key == null)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"success\":false,\"error\":\"NOT_FOUND\",\"message\":\"no fake response\"}", Encoding.UTF8, "application/json")
            };
        }

        return new HttpResponseMessage(vMatch.Value.Status)
        {
            Content = new StringContent(vMatch.Value.Json, Encoding.UTF8, "application/json")
        };
    }
}
