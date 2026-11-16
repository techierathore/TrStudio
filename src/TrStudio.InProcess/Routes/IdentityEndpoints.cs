// Ported from src/TrStudioApi/Endpoints/IdentityEndpoints.cs (phase 3, ADR-015): the same routes, answered in process.
using System.Security.Claims;
using Microsoft.Extensions.Options;
using TrStudio.Engine.Domain.Identity;
using TrStudio.Engine.Infrastructure.Identity;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Services;

using Microsoft.Extensions.DependencyInjection;
using TrStudio.InProcess.Hosting;

namespace TrStudio.InProcess.Routes;

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
        vAuth.MapPost("/resume", ResumeAsync).AllowAnonymous().WithName("PostAuthResume");
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
        SignInSettings aSignIn,
        LocalSessionStore aSessions,
        RememberedSignInStore aRemembered,
        TrStudio.Engine.Infrastructure.Security.IKeyProtector aKeyProtector,
        CancellationToken aCancellationToken)
    {
        // Phase 3 (REQ-UI-098): a build without its AppManager id and key cannot sign anyone in.
        if (!aSignIn.IsConfigured)
        {
            return Results.Problem(statusCode: 503, title: "SIGN_IN_NOT_CONFIGURED", detail: DesktopSessionEndpoints.MissingSettingsMessage);
        }

        if (string.IsNullOrWhiteSpace(aRequest.Email) || string.IsNullOrWhiteSpace(aRequest.Password))
        {
            return Results.Problem(statusCode: 400, title: "VALIDATION_ERROR", detail: "Email and password are required.");
        }

        try
        {
            var vSession = await aClient.LoginAsync(aRequest, aCancellationToken);
            aSessions.SignIn(vSession.Token, vSession.UserId, aRequest.Email, vSession.Role);

            // Owner decision 6B (REQ-FN-099, REQ-UI-105): ticked keeps the refresh token for 30 days,
            // protected for this user; not ticked keeps nothing (and forgets an older kept sign-in).
            string? vNotKept = null;
            if (aRequest.RememberMe && !string.IsNullOrWhiteSpace(vSession.RefreshToken))
            {
                try
                {
                    aRemembered.Remember(vSession.RefreshToken, aRequest.Email);
                }
                catch (Exception vException) when (vException is not OperationCanceledException)
                {
                    // Owner run 16 Nov 2026: an unsigned Mac build is refused by the Keychain. The person is
                    // still signed in, for this session only, and told why.
                    vNotKept = $"You are signed in, but this computer could not keep the sign-in: {aKeyProtector.Description} refused ({vException.Message}). "
                        + "You will be asked to sign in again next time. On a Mac this happens when the app is not signed.";
                }
            }
            else
            {
                aRemembered.Forget();
            }

            var vResult = await ApplySetupGateAsync(vSession, aSetupGate, aCancellationToken);
            vResult.NotKeptReason = vNotKept;

            return Results.Ok(vResult);
        }
        catch (UnauthorizedAccessException vException)
        {
            return Results.Problem(statusCode: 401, title: "INVALID_CREDENTIALS", detail: vException.Message);
        }
        catch (Exception vException) when (IsUnreachable(vException, aCancellationToken))
        {
            // Phase 3 (REQ-UI-099): AppManager cannot be reached; say so in plain words.
            return Results.Problem(statusCode: 503, title: "SIGN_IN_UNREACHABLE", detail: DesktopSessionEndpoints.UnreachableMessage);
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
        SignInSettings aSignIn,
        LocalSessionStore aSessions,
        CancellationToken aCancellationToken)
    {
        if (!aSignIn.IsConfigured)
        {
            return Results.Problem(statusCode: 503, title: "SIGN_IN_NOT_CONFIGURED", detail: DesktopSessionEndpoints.MissingSettingsMessage);
        }

        if (string.IsNullOrWhiteSpace(aRequest.Email) || string.IsNullOrWhiteSpace(aRequest.Password) || string.IsNullOrWhiteSpace(aRequest.Name))
        {
            return Results.Problem(statusCode: 400, title: "VALIDATION_ERROR", detail: "Name, email and password are required.");
        }

        try
        {
            var vSession = await aClient.RegisterAsync(aRequest, aCancellationToken);
            aSessions.SignIn(vSession.Token, vSession.UserId, aRequest.Email, vSession.Role);
            return Results.Ok(await ApplySetupGateAsync(vSession, aSetupGate, aCancellationToken));
        }
        catch (UnauthorizedAccessException vException)
        {
            return Results.Problem(statusCode: 401, title: "UNAUTHORIZED", detail: vException.Message);
        }
        catch (Exception vException) when (IsUnreachable(vException, aCancellationToken))
        {
            return Results.Problem(statusCode: 503, title: "SIGN_IN_UNREACHABLE", detail: DesktopSessionEndpoints.UnreachableMessage);
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
    /// Signs in with the sign-in kept on this computer when "Remember me" was ticked (owner decision
    /// 6B, REQ-FN-100, REQ-FN-101): 204 when nothing is kept; the session when AppManager renews the
    /// kept token; 401 <c>SIGN_IN_ENDED</c> with plain words when the 30 days are over or AppManager
    /// refuses or has revoked the token (the kept file is removed); 503 when AppManager cannot be
    /// reached (the kept file stays, so the next open tries again).
    /// </summary>
    private static async Task<IResult> ResumeAsync(
        IAppManagerClient aClient,
        ISetupGateService aSetupGate,
        SignInSettings aSignIn,
        LocalSessionStore aSessions,
        RememberedSignInStore aRemembered,
        CancellationToken aCancellationToken)
    {
        if (!aSignIn.IsConfigured)
        {
            return Results.NoContent();
        }

        var vKept = aRemembered.Read();
        if (vKept is null)
        {
            return Results.NoContent();
        }

        if (aRemembered.HasEnded(vKept))
        {
            aRemembered.Forget();
            return Results.Problem(statusCode: 401, title: "SIGN_IN_ENDED", detail: DesktopSessionEndpoints.KeptSignInEndedMessage);
        }

        try
        {
            var vRenewed = await aClient.RefreshAsync(vKept.RefreshToken, aCancellationToken);
            var vProfile = await aClient.GetProfileAsync(vRenewed.Token, aCancellationToken);
            aRemembered.Renew(vKept, vRenewed.RefreshToken);
            var vEmail = string.IsNullOrWhiteSpace(vProfile.Email) ? vKept.Email : vProfile.Email;
            aSessions.SignIn(vRenewed.Token, vProfile.UserId, vEmail, vProfile.Role);
            var vSession = new LoginResponse
            {
                Token = vRenewed.Token,
                RefreshToken = vRenewed.RefreshToken,
                UserId = vProfile.UserId,
                Role = vProfile.Role
            };
            return Results.Ok(new ResumedSignIn(await ApplySetupGateAsync(vSession, aSetupGate, aCancellationToken), vEmail));
        }
        catch (Exception vException) when (IsUnreachable(vException, aCancellationToken)
                                          || vException is AppManagerApiException { ErrorCode: "APPMANAGER_UNREACHABLE" or "APPMANAGER_TIMEOUT" })
        {
            return Results.Problem(statusCode: 503, title: "SIGN_IN_UNREACHABLE", detail: DesktopSessionEndpoints.UnreachableMessage);
        }
        catch (Exception vException) when (vException is UnauthorizedAccessException or AppManagerApiException { StatusCode: 400 or 401 or 403 })
        {
            // Expired, revoked, unknown or issued for another app: AppManager refuses it. Forget it.
            aRemembered.Forget();
            return Results.Problem(statusCode: 401, title: "SIGN_IN_ENDED", detail: DesktopSessionEndpoints.KeptSignInRefusedMessage);
        }
    }

    /// <summary>The session a kept sign-in opened, with the email for the shell chip.</summary>
    /// <param name="Session">The session.</param>
    /// <param name="Email">The sign-in email.</param>
    private sealed record ResumedSignIn(LoginResponse Session, string? Email);

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

    private static bool IsUnreachable(Exception aException, CancellationToken aCancellationToken)
        => aException is HttpRequestException
           || (aException is TaskCanceledException && !aCancellationToken.IsCancellationRequested);

    private static IResult ToProblem(AppManagerApiException aException)
    {
        // Phase 3 (REQ-UI-099): the client turns a refused connection or a timeout into these codes;
        // the screen shows the plain sign-in-service message, never an address.
        if (aException.ErrorCode is "APPMANAGER_UNREACHABLE" or "APPMANAGER_TIMEOUT")
        {
            return Results.Problem(statusCode: 503, title: "SIGN_IN_UNREACHABLE", detail: DesktopSessionEndpoints.UnreachableMessage);
        }

        return Results.Problem(statusCode: aException.StatusCode, title: aException.ErrorCode, detail: aException.Message);
    }
}
