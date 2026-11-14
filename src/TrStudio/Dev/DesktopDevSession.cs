using TrStudio.InProcess;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;
using TrStudioUI.Auth;

namespace TrStudio.Dev;

/// <summary>
/// DEBUG-only developer session for the desktop head (REQ-NFR-015): opens the app already signed in
/// with a chosen role, for a developer without AppManager at hand. Phase 3: the session is a random
/// token registered with the in-process engine; no token is signed and no signing key exists
/// (ADR-019). Release builds never enable it.
/// </summary>
public static class DesktopDevSession
{
    /// <summary>True only in DEBUG builds.</summary>
    public static bool IsEnabled =>
#if DEBUG
        true;
#else
        false;
#endif

    /// <summary>The screen to open first, when the developer named one.</summary>
    public static string? RequestedNavPath { get; set; }

    /// <summary>The engine the developer session registers with (set by MauiProgram).</summary>
    public static EngineHost? Host { get; set; }

    /// <summary>Builds and registers a developer session for a role.</summary>
    /// <param name="aRole">The role.</param>
    /// <returns>The state for the desktop session.</returns>
    public static AuthSessionState BuildDevState(UserRole aRole)
    {
        var vToken = Register(aRole);
        return new AuthSessionState(vToken, string.Empty, UserIdFor(aRole), aRole, LandingFor(aRole));
    }

    /// <summary>Signs the desktop in as a developer of the given role.</summary>
    /// <param name="aAuthSession">The desktop session.</param>
    /// <param name="aRole">The role.</param>
    /// <returns>The landing path.</returns>
    public static async Task<string> EstablishAsync(IAuthSession aAuthSession, UserRole aRole)
    {
        ArgumentNullException.ThrowIfNull(aAuthSession);
        if (!IsEnabled)
        {
            return "/";
        }

        var vLanding = LandingFor(aRole);
        await aAuthSession.SetAsync(new LoginResponse
        {
            Token = Register(aRole),
            RefreshToken = string.Empty,
            UserId = UserIdFor(aRole),
            Role = aRole,
            DefaultLandingPath = vLanding
        }).ConfigureAwait(false);

        return vLanding;
    }

    /// <summary>Parses a role name; Creator when unknown.</summary>
    /// <param name="aRoleName">The name.</param>
    /// <returns>The role.</returns>
    public static UserRole ParseRole(string? aRoleName) =>
        Enum.TryParse<UserRole>(aRoleName, ignoreCase: true, out var vRole) ? vRole : UserRole.Creator;

    /// <summary>The landing page of a role.</summary>
    /// <param name="aRole">The role.</param>
    /// <returns>The path.</returns>
    public static string LandingFor(UserRole aRole) =>
        aRole == UserRole.Operator ? "/admin/ops" : "/avatar";

    private static string UserIdFor(UserRole aRole) => $"dev-{aRole}".ToLowerInvariant();

    private static string Register(UserRole aRole)
    {
        var vToken = "dev-" + Guid.NewGuid().ToString("N");
        Host?.Sessions.SignIn(vToken, UserIdFor(aRole), $"{aRole}@trstudio.dev".ToLowerInvariant(), aRole);
        return vToken;
    }
}
