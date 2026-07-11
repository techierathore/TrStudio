using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TrStudio.Engine.Domain.ApiKeys;

namespace TrStudioApi.Infrastructure.Auth;

/// <summary>
/// Authentication handler for the external <c>X-Api-Key</c> scheme (REQ-FN-069/BRD-66). It resolves
/// the presented key to an owning user + scope via <see cref="IApiKeyService"/> and issues a
/// ClaimsPrincipal carrying that user id — letting non-interactive callers reach the same API as an
/// interactive JWT session. A missing header defers (so the composite scheme can fall back to JWT);
/// a present-but-invalid key fails the request. The JWT path is never touched.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    /// <summary>The role granted to API-key callers — deliberately non-admin (admin ops stay JWT-only).</summary>
    private const string ApiKeyRole = "Creator";

    private readonly IApiKeyService objApiKeyService;

    /// <summary>
    /// Initialises the handler.
    /// </summary>
    /// <param name="aOptions">The scheme options monitor.</param>
    /// <param name="aLogger">The logger factory.</param>
    /// <param name="aEncoder">The URL encoder.</param>
    /// <param name="aApiKeyService">The API-key lookup/authentication service.</param>
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> aOptions,
        ILoggerFactory aLogger,
        UrlEncoder aEncoder,
        IApiKeyService aApiKeyService)
        : base(aOptions, aLogger, aEncoder)
    {
        objApiKeyService = aApiKeyService;
    }

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyDefaults.HeaderName, out var vHeaderValues))
        {
            // No API key presented — let the composite scheme fall through to JWT.
            return AuthenticateResult.NoResult();
        }

        var vPresentedKey = vHeaderValues.ToString();
        var vResult = await objApiKeyService.AuthenticateAsync(vPresentedKey, Context.RequestAborted).ConfigureAwait(false);
        if (vResult is null)
        {
            return AuthenticateResult.Fail("The presented API key is invalid or has been revoked.");
        }

        var vClaims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, vResult.UserId),
            new Claim("sub", vResult.UserId),
            new Claim("scope", vResult.Scope),
            new Claim("role", ApiKeyRole),
            new Claim("auth_method", "api-key"),
            new Claim("api_key_id", vResult.ApiKeyId.ToString())
        };

        var vIdentity = new ClaimsIdentity(vClaims, ApiKeyDefaults.Scheme);
        var vPrincipal = new ClaimsPrincipal(vIdentity);
        var vTicket = new AuthenticationTicket(vPrincipal, ApiKeyDefaults.Scheme);
        return AuthenticateResult.Success(vTicket);
    }
}
