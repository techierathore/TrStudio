using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;
using TrStudioUI.Auth;

namespace TrStudio.Desktop.Dev;

/// <summary>
/// DEV-ONLY desktop session seam. A live AppManager is required for a real session token; on the
/// desktop head there is no <c>/dev/session</c> web page to paste into, so this helper mints an
/// HS256 dev token for a chosen <see cref="UserRole"/> — signed with the same symmetric dev key the
/// API accepts behind <c>AppManager:AllowDevSymmetricTokens</c> — and stores it via the Hybrid
/// <see cref="IAuthSession"/>. That lets the desktop app be smoked as Creator / Operator / Owner.
/// Everything here is gated on <see cref="IsEnabled"/> (a DEBUG build), so it is inert in Release.
/// </summary>
public static class DesktopDevSession
{
    /// <summary>
    /// The symmetric dev signing key the API validates when <c>AppManager:JwtSigningKey</c> is unset
    /// (the Wave-0 placeholder). Mirrors <c>TrStudioApi/Program.cs</c>; a dev-only convenience, never
    /// a production secret.
    /// </summary>
    private const string DevSigningKey = "TrStudioWave0PlaceholderSigningKeyReplacedByIdentityCluster";

    /// <summary>Whether the dev seam is active — true only for a DEBUG build.</summary>
    public static bool IsEnabled =>
#if DEBUG
        true;
#else
        false;
#endif

    /// <summary>
    /// The client-side route the root initializer navigates to once, after the DEBUG dev session has
    /// been pre-seeded at startup (role landing, or a <c>TrStudioDevStartPath</c> deep link). Null
    /// when no dev role was requested.
    /// </summary>
    public static string? RequestedNavPath { get; set; }

    /// <summary>
    /// Builds a pre-seeded dev session state (minted token + resolved role/landing) for
    /// <paramref name="aRole"/>, for synchronous seeding before the WebView's first render.
    /// </summary>
    /// <param name="aRole">The role to establish.</param>
    /// <returns>The session state to seed.</returns>
    public static AuthSessionState BuildDevState(UserRole aRole) => new(
        MintToken(aRole),
        string.Empty,
        $"dev-{aRole}".ToLowerInvariant(),
        aRole,
        LandingFor(aRole));

    /// <summary>
    /// Mints a dev token for <paramref name="aRole"/>, stores it via <paramref name="aAuthSession"/>,
    /// and returns the role-appropriate landing path. No-op returning "/" when the seam is disabled.
    /// </summary>
    /// <param name="aAuthSession">The Hybrid session store to persist the minted session into.</param>
    /// <param name="aRole">The role to establish.</param>
    /// <returns>The landing path for the established role.</returns>
    public static async Task<string> EstablishAsync(IAuthSession aAuthSession, UserRole aRole)
    {
        ArgumentNullException.ThrowIfNull(aAuthSession);
        if (!IsEnabled)
        {
            return "/";
        }

        var vLanding = LandingFor(aRole);
        var vToken = MintToken(aRole);

        await aAuthSession.SetAsync(new LoginResponse
        {
            Token = vToken,
            RefreshToken = string.Empty,
            UserId = $"dev-{aRole}".ToLowerInvariant(),
            Role = aRole,
            DefaultLandingPath = vLanding
        }).ConfigureAwait(false);

        return vLanding;
    }

    /// <summary>Parses a role name (case-insensitive) to a <see cref="UserRole"/>; defaults to Creator.</summary>
    /// <param name="aRoleName">The role name (e.g. "Owner", "Operator", "Creator").</param>
    /// <returns>The parsed role.</returns>
    public static UserRole ParseRole(string? aRoleName) =>
        Enum.TryParse<UserRole>(aRoleName, ignoreCase: true, out var vRole) ? vRole : UserRole.Creator;

    /// <summary>
    /// Builds an HS256 JWT carrying the claims the API resolves a role from
    /// (<c>userId</c>/<c>sub</c>, <c>email</c>, <c>applicationRole</c>/<c>role</c>). Valid for 8h.
    /// </summary>
    private static string MintToken(UserRole aRole)
    {
        var vUserId = $"dev-{aRole}".ToLowerInvariant();
        var vEmail = $"{aRole}@trstudio.dev".ToLowerInvariant();
        var vRoleCode = aRole.ToString();

        var vClaims = new[]
        {
            new Claim("sub", vUserId),
            new Claim("userId", vUserId),
            new Claim(ClaimTypes.NameIdentifier, vUserId),
            new Claim("email", vEmail),
            new Claim(ClaimTypes.Email, vEmail),
            new Claim("applicationRole", vRoleCode),
            new Claim("role", vRoleCode)
        };

        var vCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(DevSigningKey)),
            SecurityAlgorithms.HmacSha256);

        var vNow = DateTime.UtcNow;
        var vToken = new JwtSecurityToken(
            claims: vClaims,
            notBefore: vNow,
            expires: vNow.AddHours(8),
            signingCredentials: vCredentials);

        return new JwtSecurityTokenHandler().WriteToken(vToken);
    }

    /// <summary>Role-appropriate landing: Operator opens on management; the rest on the creator studio.</summary>
    public static string LandingFor(UserRole aRole) =>
        aRole == UserRole.Operator ? "/admin/ops" : "/avatar";
}
