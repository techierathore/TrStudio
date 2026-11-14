using System.Collections.Concurrent;
using System.Security.Claims;
using TrStudio.Models.Enums;

namespace TrStudio.InProcess.Hosting;

/// <summary>
/// The signed-in users of this app, held in memory for the session (phase 3, ADR-019). Signing in
/// through AppManager adds the user under the session token the screens send back as a bearer
/// token; signing out removes it, after which every protected route answers 401 (REQ-FN-088).
/// Nothing here is written to disk.
/// </summary>
public sealed class LocalSessionStore
{
    private readonly ConcurrentDictionary<string, ClaimsPrincipal> objSessions = new(StringComparer.Ordinal);

    /// <summary>Records a signed-in user.</summary>
    /// <param name="aToken">The session token (the AppManager access token).</param>
    /// <param name="aUserId">The AppManager user id.</param>
    /// <param name="aEmail">The email, when known.</param>
    /// <param name="aRole">The TrStudio role.</param>
    public void SignIn(string aToken, string aUserId, string? aEmail, UserRole aRole)
    {
        var vClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, aUserId),
            new("sub", aUserId),
            new(ClaimTypes.Role, aRole.ToString()),
            new("role", aRole.ToString())
        };
        if (!string.IsNullOrWhiteSpace(aEmail))
        {
            vClaims.Add(new Claim(ClaimTypes.Email, aEmail));
            vClaims.Add(new Claim("email", aEmail));
        }

        objSessions[aToken] = new ClaimsPrincipal(new ClaimsIdentity(vClaims, "TrStudioLocalSession"));
    }

    /// <summary>Ends a session.</summary>
    /// <param name="aToken">The session token.</param>
    /// <returns>True when a session was ended.</returns>
    public bool SignOut(string aToken) => objSessions.TryRemove(aToken, out _);

    /// <summary>The user of a session token, or an unauthenticated principal.</summary>
    /// <param name="aToken">The session token, or null.</param>
    /// <returns>The principal.</returns>
    public ClaimsPrincipal Resolve(string? aToken)
        => aToken is not null && objSessions.TryGetValue(aToken, out var vUser) ? vUser : new ClaimsPrincipal(new ClaimsIdentity());

    /// <summary>How many sessions are open.</summary>
    public int Count => objSessions.Count;
}
