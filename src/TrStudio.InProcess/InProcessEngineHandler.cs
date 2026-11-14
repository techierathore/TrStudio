using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrStudio.InProcess.Hosting;

namespace TrStudio.InProcess;

/// <summary>
/// The primary handler of the screens' named <c>TrStudioApi</c> HttpClient in the desktop app and the
/// test host (phase 3, ADR-015). It answers <c>/api/v1/...</c> calls in this process from the route
/// table: no socket is opened and no web server runs (REQ-NFR-016). The bearer token the screens send
/// selects the signed-in user from <see cref="LocalSessionStore"/>; a protected route without one
/// answers 401 (REQ-FN-088).
/// </summary>
public sealed class InProcessEngineHandler : HttpMessageHandler
{
    /// <summary>The base address the screens' client uses; it is never resolved on a network.</summary>
    public static readonly Uri BaseAddress = new("http://trstudio.local/");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly EngineHost objHost;

    /// <summary>Creates the handler over a started engine host.</summary>
    /// <param name="aHost">The engine host.</param>
    public InProcessEngineHandler(EngineHost aHost) => objHost = aHost;

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
    {
        var vResponse = await AnswerAsync(aRequest, aCancellationToken).ConfigureAwait(false);
        vResponse.RequestMessage = aRequest;
        return vResponse;
    }

    private async Task<HttpResponseMessage> AnswerAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
    {
        var vUri = aRequest.RequestUri ?? throw new InvalidOperationException("The request has no address.");
        var vPath = vUri.IsAbsoluteUri ? vUri.AbsolutePath : vUri.OriginalString.Split('?')[0];

        if (objHost.StartupProblem is { } vProblem)
        {
            return Results.Problem(statusCode: 503, title: "LOCAL_STORE_UNAVAILABLE", detail: vProblem).ToResponse(Json);
        }

        var vRoute = objHost.Routes.Find(aRequest.Method.Method, vPath, out var vRouteValues, out var vPathExists);
        if (vRoute is null)
        {
            return new HttpResponseMessage(vPathExists ? HttpStatusCode.MethodNotAllowed : HttpStatusCode.NotFound);
        }

        var vHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var vHeader in aRequest.Headers)
        {
            vHeaders[vHeader.Key] = string.Join(",", vHeader.Value);
        }

        var vToken = aRequest.Headers.Authorization is { Scheme: var vScheme, Parameter: var vParameter }
            && string.Equals(vScheme, "Bearer", StringComparison.OrdinalIgnoreCase) ? vParameter : null;
        var vUser = objHost.Sessions.Resolve(vToken);
        var vContext = new Hosting.HttpContext(vUser, vHeaders);

        if (!vRoute.Anonymous && vUser.Identity?.IsAuthenticated != true)
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        await using var vScope = objHost.Services.CreateAsyncScope();
        if (vRoute.Policies.Count > 0)
        {
            var vAuthorization = vScope.ServiceProvider.GetRequiredService<IAuthorizationService>();
            foreach (var vPolicy in vRoute.Policies)
            {
                var vResult = await vAuthorization.AuthorizeAsync(vUser, vPolicy).ConfigureAwait(false);
                if (!vResult.Succeeded)
                {
                    return new HttpResponseMessage(HttpStatusCode.Forbidden);
                }
            }
        }

        var vQuery = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (vUri.IsAbsoluteUri && vUri.Query.Length > 1)
        {
            var vParsed = HttpUtility.ParseQueryString(vUri.Query);
            foreach (var vKey in vParsed.AllKeys)
            {
                if (vKey is not null)
                {
                    vQuery[vKey] = vParsed[vKey] ?? string.Empty;
                }
            }
        }

        var vBody = aRequest.Content is null ? null : await aRequest.Content.ReadAsStringAsync(aCancellationToken).ConfigureAwait(false);

        try
        {
            var vResult = await RouteInvoker.InvokeAsync(vRoute, vScope.ServiceProvider, vContext, vRouteValues, vQuery, vBody, Json, aCancellationToken).ConfigureAwait(false);
            var vResponse = vResult.ToResponse(Json);
            foreach (var vExtra in vContext.Response.Headers)
            {
                vResponse.Headers.TryAddWithoutValidation(vExtra.Key, vExtra.Value);
            }

            return vResponse;
        }
        catch (JsonException vException)
        {
            return Results.Problem(statusCode: 400, title: "INVALID_REQUEST", detail: vException.Message).ToResponse(Json);
        }
        catch (OperationCanceledException) when (aCancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception vException)
        {
            objHost.Logger.LogError(vException, "In-process route {Method} {Template} failed", aRequest.Method.Method, vRoute.Template);
            return Results.Problem(statusCode: 500, title: "ENGINE_ERROR", detail: vException.Message).ToResponse(Json);
        }
    }
}
