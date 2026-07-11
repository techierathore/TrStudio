using System.Net.Http.Headers;
using TrStudioUI.Auth;

namespace TrStudio.Desktop.Auth;

/// <summary>
/// The MAUI Hybrid counterpart of the web head's bearer <c>DelegatingHandler</c>: it stamps every
/// outgoing named-<c>TrStudioApi</c> request with the signed-in user's <c>Authorization: Bearer</c>
/// token so the shared screens' owner-scoped API calls (assets, projects, models, admin/*) are
/// authenticated. Unlike the web handler — which bridged the Blazor <i>circuit</i> DI scope via a
/// circuit-services accessor — this handler injects the <b>singleton</b> <see cref="IAuthSession"/>
/// directly, because a Hybrid host has one process-wide session and no circuits. The token is never
/// logged nor placed in a URL (NFR-05).
/// </summary>
public sealed class MauiAuthTokenHandler : DelegatingHandler
{
    private readonly IAuthSession objAuthSession;

    /// <summary>Initialises the handler with the process-wide auth session.</summary>
    /// <param name="aAuthSession">The singleton SecureStorage-backed session.</param>
    public MauiAuthTokenHandler(IAuthSession aAuthSession)
    {
        objAuthSession = aAuthSession;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage aRequest,
        CancellationToken aCancellationToken)
    {
        if (aRequest.Headers.Authorization is null)
        {
            var vToken = await objAuthSession.GetTokenAsync().ConfigureAwait(false);
            if (!string.IsNullOrEmpty(vToken))
            {
                aRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", vToken);
            }
        }

        return await base.SendAsync(aRequest, aCancellationToken).ConfigureAwait(false);
    }
}
