using System.Security.Claims;
using Microsoft.Extensions.Options;
using TrStudio.Engine.Domain.Identity;
using TrStudio.Engine.Infrastructure.Identity;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Services;

namespace TrStudioApi.Endpoints;

/// <summary>
/// Endpoint mapping for the identity cluster: authentication (<c>/api/v1/auth</c> — login,
/// register, refresh, forgot-password, public-key, me) forwarded to AppManager, the license
/// status endpoint (<c>/api/v1/license</c>) and the first-run setup gate
/// (<c>/api/v1/setup-gate</c>). No password is ever stored or logged locally (BRD-57).
/// </summary>
/// <remarks>
/// OWNED BY: cluster D (identity). Only that cluster edits this file; other clusters map their
/// endpoints in their own file. Auth endpoints are marked <c>AllowAnonymous</c>.
/// </remarks>
public static class IdentityEndpoints
{
    /// <summary>
    /// Maps the identity endpoints onto the application pipeline.
    /// </summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapIdentityEndpoints(this WebApplication aApp)
    {
        var vAuth = aApp.MapGroup(ApiRoutes.Auth);
        vAuth.MapPost("/login", LoginAsync).AllowAnonymous().WithName("PostAuthLogin");
        vAuth.MapPost("/register", RegisterAsync).AllowAnonymous().WithName("PostAuthRegister");
        vAuth.MapPost("/refresh", RefreshAsync).AllowAnonymous().WithName("PostAuthRefresh");
        vAuth.MapPost("/forgot-password", ForgotPasswordAsync).AllowAnonymous().WithName("PostAuthForgotPassword");
        vAuth.MapGet("/public-key", GetPublicKeyAsync).AllowAnonymous().WithName("GetAuthPublicKey");
        vAuth.MapGet("/me", GetMeAsync).RequireAuthorization().WithName("GetAuthMe");

        aApp.MapGet(ApiRoutes.SetupGate, GetSetupGateAsync).RequireAuthorization().WithName("GetSetupGate");
        aApp.MapGet(ApiRoutes.License, GetLicenseStatusAsync).RequireAuthorization().WithName("GetLicenseStatus");
        return aApp;
    }

    /// <summary>
    /// Authenticates a user against AppManager (RSA-encrypted password) and returns the session
    /// with the setup-gate-aware default landing path (REQ-FN-012/016/017).
    /// </summary>
    private static async Task<IResult> LoginAsync(
        LoginRequest aRequest,
        IAppManagerClient aClient,
        ISetupGateService aSetupGate,
        CancellationToken aCancellationToken)
    {
        if (string.IsNullOrWhiteSpace(aRequest.Email) || string.IsNullOrWhiteSpace(aRequest.Password))
        {
            return Results.Problem(statusCode: 400, title: "VALIDATION_ERROR", detail: "Email and password are required.");
        }

        try
        {
            var vSession = await aClient.LoginAsync(aRequest, aCancellationToken);
            return Results.Ok(await ApplySetupGateAsync(vSession, aSetupGate, aCancellationToken));
        }
        catch (UnauthorizedAccessException vException)
        {
            return Results.Problem(statusCode: 401, title: "INVALID_CREDENTIALS", detail: vException.Message);
        }
        catch (AppManagerApiException vException)
        {
            return ToProblem(vException);
        }
    }

    /// <summary>
    /// Registers a new user via the AppManager child-app API (REQ-FN-015); the account type maps
    /// to the provisioned application role. A duplicate email surfaces AppManager's EMAIL_EXISTS.
    /// </summary>
    private static async Task<IResult> RegisterAsync(
        RegisterRequest aRequest,
        IAppManagerClient aClient,
        ISetupGateService aSetupGate,
        CancellationToken aCancellationToken)
    {
        if (string.IsNullOrWhiteSpace(aRequest.Email) || string.IsNullOrWhiteSpace(aRequest.Password) || string.IsNullOrWhiteSpace(aRequest.Name))
        {
            return Results.Problem(statusCode: 400, title: "VALIDATION_ERROR", detail: "Name, email and password are required.");
        }

        try
        {
            var vSession = await aClient.RegisterAsync(aRequest, aCancellationToken);
            return Results.Ok(await ApplySetupGateAsync(vSession, aSetupGate, aCancellationToken));
        }
        catch (UnauthorizedAccessException vException)
        {
            return Results.Problem(statusCode: 401, title: "UNAUTHORIZED", detail: vException.Message);
        }
        catch (AppManagerApiException vException)
        {
            return ToProblem(vException);
        }
    }

    /// <summary>
    /// Renews a session using an AppManager refresh token (REQ-FN-012).
    /// </summary>
    private static async Task<IResult> RefreshAsync(
        RefreshTokenRequest aRequest,
        IAppManagerClient aClient,
        CancellationToken aCancellationToken)
    {
        if (string.IsNullOrWhiteSpace(aRequest.RefreshToken))
        {
            return Results.Problem(statusCode: 400, title: "VALIDATION_ERROR", detail: "A refresh token is required.");
        }

        try
        {
            return Results.Ok(await aClient.RefreshAsync(aRequest.RefreshToken, aCancellationToken));
        }
        catch (UnauthorizedAccessException vException)
        {
            return Results.Problem(statusCode: 401, title: "INVALID_REFRESH_TOKEN", detail: vException.Message);
        }
        catch (AppManagerApiException vException)
        {
            return ToProblem(vException);
        }
    }

    /// <summary>
    /// Initiates an AppManager password reset (REQ-FN-015); responds generically on success to
    /// prevent email enumeration. No local password store exists.
    /// </summary>
    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest aRequest,
        IAppManagerClient aClient,
        CancellationToken aCancellationToken)
    {
        if (string.IsNullOrWhiteSpace(aRequest.Email))
        {
            return Results.Problem(statusCode: 400, title: "VALIDATION_ERROR", detail: "An email address is required.");
        }

        try
        {
            await aClient.RequestPasswordResetAsync(aRequest, aCancellationToken);
            return Results.Ok(new { message = "If the account exists, a password reset email has been sent." });
        }
        catch (AppManagerApiException vException)
        {
            return ToProblem(vException);
        }
    }

    /// <summary>
    /// Proxies the AppManager RSA public key so UI clients can encrypt passwords client-side
    /// (guide §2.4) without talking to AppManager directly.
    /// </summary>
    private static async Task<IResult> GetPublicKeyAsync(IAppManagerClient aClient, CancellationToken aCancellationToken)
    {
        try
        {
            var vPublicKey = await aClient.GetPublicKeyAsync(aCancellationToken);
            return Results.Ok(new { publicKey = vPublicKey, algorithm = "RSA-OAEP-256" });
        }
        catch (AppManagerApiException vException)
        {
            return ToProblem(vException);
        }
    }

    /// <summary>
    /// Returns the caller's profile, resolved role and setup-gate-aware landing path (REQ-FN-016).
    /// </summary>
    private static async Task<IResult> GetMeAsync(
        ClaimsPrincipal aUser,
        HttpContext aContext,
        IAppManagerClient aClient,
        ISetupGateService aSetupGate,
        IOptions<AppManagerOptions> aOptions,
        CancellationToken aCancellationToken)
    {
        var vProfile = await ResolveProfileAsync(aUser, aContext, aClient, aOptions.Value, aCancellationToken);
        var vGate = await aSetupGate.EvaluateAsync(vProfile.UserId, vProfile.Role, aCancellationToken);
        vProfile.SetupComplete = vGate.SetupComplete;
        vProfile.DefaultLandingPath = vGate.RedirectPath;
        return Results.Ok(vProfile);
    }

    /// <summary>
    /// Evaluates the first-run setup gate for the caller (REQ-FN-017): complete ⇔ ≥1 enabled
    /// model source AND ≥1 ready registry model; the redirect path is role-appropriate.
    /// </summary>
    private static async Task<IResult> GetSetupGateAsync(
        ClaimsPrincipal aUser,
        HttpContext aContext,
        IAppManagerClient aClient,
        ISetupGateService aSetupGate,
        IOptions<AppManagerOptions> aOptions,
        CancellationToken aCancellationToken)
    {
        var vProfile = await ResolveProfileAsync(aUser, aContext, aClient, aOptions.Value, aCancellationToken);
        var vGate = await aSetupGate.EvaluateAsync(vProfile.UserId, vProfile.Role, aCancellationToken);
        return Results.Ok(vGate);
    }

    /// <summary>
    /// Reports the caller's AppManager license validity (REQ-FN-013); an expired or absent
    /// license is reported with a renewal message, never a server error.
    /// </summary>
    private static async Task<IResult> GetLicenseStatusAsync(
        HttpContext aContext,
        IAppManagerClient aClient,
        CancellationToken aCancellationToken)
    {
        var vToken = GetBearerToken(aContext);
        if (vToken == null)
        {
            return Results.Problem(statusCode: 401, title: "UNAUTHORIZED", detail: "A bearer token is required.");
        }

        try
        {
            return Results.Ok(await aClient.GetLicenseStatusAsync(vToken, aCancellationToken));
        }
        catch (UnauthorizedAccessException vException)
        {
            return Results.Problem(statusCode: 401, title: "UNAUTHORIZED", detail: vException.Message);
        }
        catch (AppManagerApiException vException)
        {
            return ToProblem(vException);
        }
    }

    /// <summary>
    /// Resolves the caller's identity: claims first (works for locally-validated dev tokens);
    /// when the claims carry no role, the AppManager profile endpoint is consulted for fidelity.
    /// </summary>
    private static async Task<UserProfileResponse> ResolveProfileAsync(
        ClaimsPrincipal aUser,
        HttpContext aContext,
        IAppManagerClient aClient,
        AppManagerOptions aOptions,
        CancellationToken aCancellationToken)
    {
        var vUserId = FirstClaim(aUser, ClaimTypes.NameIdentifier, "sub", "userId", "nameid") ?? string.Empty;
        var vEmail = FirstClaim(aUser, ClaimTypes.Email, "email") ?? string.Empty;
        var vRoleClaim = FirstClaim(aUser, ClaimTypes.Role, "role", "applicationRole");

        if (vRoleClaim == null)
        {
            var vRemoteProfile = await TryGetRemoteProfileAsync(aContext, aClient, aCancellationToken);
            if (vRemoteProfile != null)
            {
                return vRemoteProfile;
            }
        }

        return new UserProfileResponse
        {
            UserId = vUserId,
            Email = vEmail,
            Role = UserRoleMapper.MapRole(vEmail, null, vRoleClaim, aOptions.SuperAdminEmail)
        };
    }

    /// <summary>
    /// Attempts to fetch the caller's AppManager profile with their own bearer token; returns
    /// null when the token is not an AppManager token (e.g. a locally-signed dev token).
    /// </summary>
    private static async Task<UserProfileResponse?> TryGetRemoteProfileAsync(
        HttpContext aContext,
        IAppManagerClient aClient,
        CancellationToken aCancellationToken)
    {
        var vToken = GetBearerToken(aContext);
        if (vToken == null)
        {
            return null;
        }

        try
        {
            return await aClient.GetProfileAsync(vToken, aCancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (AppManagerApiException)
        {
            return null;
        }
    }

    private static string? FirstClaim(ClaimsPrincipal aUser, params string[] aClaimTypes)
    {
        return aClaimTypes
            .Select(aClaimType => aUser.FindFirst(aClaimType)?.Value)
            .FirstOrDefault(aValue => !string.IsNullOrWhiteSpace(aValue));
    }

    private static string? GetBearerToken(HttpContext aContext)
    {
        const string BearerPrefix = "Bearer ";
        var vHeader = aContext.Request.Headers.Authorization.ToString();
        return vHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? vHeader[BearerPrefix.Length..].Trim()
            : null;
    }

    private static async Task<LoginResponse> ApplySetupGateAsync(
        LoginResponse aSession,
        ISetupGateService aSetupGate,
        CancellationToken aCancellationToken)
    {
        var vGate = await aSetupGate.EvaluateAsync(aSession.UserId, aSession.Role, aCancellationToken);
        aSession.SetupComplete = vGate.SetupComplete;
        aSession.DefaultLandingPath = vGate.RedirectPath;
        return aSession;
    }

    private static IResult ToProblem(AppManagerApiException aException)
    {
        return Results.Problem(statusCode: aException.StatusCode, title: aException.ErrorCode, detail: aException.Message);
    }
}
