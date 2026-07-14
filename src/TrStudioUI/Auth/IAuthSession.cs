using TrStudio.Models.Contracts;

namespace TrStudioUI.Auth;

/// <summary>
/// The circuit-scoped authentication session. Holds the current bearer token and user claims for
/// the signed-in user, hydrating them lazily from encrypted <c>ProtectedSessionStorage</c> and
/// persisting them on sign-in so that every named-<c>TrStudioApi</c> HTTP call carries the token
/// (via <see cref="AuthTokenHandler"/>). The token is never logged nor placed in a URL (NFR-05).
/// </summary>
public interface IAuthSession
{
    /// <summary>
    /// Returns the current session state, hydrating it from encrypted session storage on first
    /// use. Returns <c>null</c> when no user is signed in (or during prerender, when the browser
    /// store is not yet reachable — the caller degrades gracefully and retries once interactive).
    /// </summary>
    Task<AuthSessionState?> GetAsync();

    /// <summary>Returns the current bearer token, or <c>null</c> when no session is present.</summary>
    Task<string?> GetTokenAsync();

    /// <summary>
    /// Ensures the session has been hydrated from encrypted session storage — a no-op once hydrated —
    /// and reports whether a signed-in session is present. Pages call this from
    /// <c>OnAfterRenderAsync(firstRender: true)</c> (when JS interop is available) BEFORE their first
    /// owner-scoped API call, so that call carries the bearer token instead of racing prerender —
    /// where no token is hydrated, the call returns 401, and the page would flash sample data.
    /// </summary>
    /// <returns><c>true</c> when a signed-in session is present; otherwise <c>false</c>.</returns>
    Task<bool> EnsureLoadedAsync();

    /// <summary>
    /// Stores the session returned by a successful login/register and persists it to encrypted
    /// session storage. No password is ever stored — only the returned tokens and claims.
    /// </summary>
    /// <param name="aSession">The successful login/register response.</param>
    /// <param name="aEmail">The sign-in email (shell-chip identity only; not returned by the API).</param>
    Task SetAsync(LoginResponse aSession, string? aEmail = null);

    /// <summary>Clears the session from memory and encrypted session storage (sign-out).</summary>
    Task ClearAsync();
}
