using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;

namespace TrStudioUI.Auth;

/// <summary>
/// A <see cref="DelegatingHandler"/> registered on the named <c>TrStudioApi</c> client that stamps
/// every outgoing request with the signed-in user's <c>Authorization: Bearer</c> token, turning the
/// previously-anonymous per-user API calls (assets, projects, models, admin/*) into authenticated,
/// owner-scoped calls. The token is read from the circuit-scoped <see cref="IAuthSession"/> via
/// <see cref="CircuitServicesAccessor"/> — never logged and never placed in a URL (NFR-05).
/// </summary>
public sealed class AuthTokenHandler : DelegatingHandler
{
    private readonly CircuitServicesAccessor objCircuitServices;

    /// <summary>Initialises the handler with the circuit-service accessor.</summary>
    /// <param name="aCircuitServices">Bridge to the current circuit's DI scope.</param>
    public AuthTokenHandler(CircuitServicesAccessor aCircuitServices)
    {
        objCircuitServices = aCircuitServices;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage aRequest,
        CancellationToken aCancellationToken)
    {
        if (aRequest.Headers.Authorization is null)
        {
            var vServices = objCircuitServices.Services;
            var vSession = vServices?.GetService<IAuthSession>();
            if (vSession is not null)
            {
                var vToken = await vSession.GetTokenAsync();
                if (!string.IsNullOrEmpty(vToken))
                {
                    aRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", vToken);
                }
            }
        }

        return await base.SendAsync(aRequest, aCancellationToken);
    }
}
