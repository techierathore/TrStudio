using TrStudio.Models.Enums;

namespace TrStudioUI.Auth;

/// <summary>
/// The persisted per-circuit authentication state: the AppManager session token, its refresh
/// token and the resolved user claims. Held in <see cref="IAuthSession"/> and encrypted at rest
/// in <c>ProtectedSessionStorage</c> (the browser never sees a plaintext token, and no password
/// is ever stored — BRD-57 / NFR-05).
/// </summary>
/// <param name="Token">The JWT bearer token attached to outgoing TrStudioApi requests.</param>
/// <param name="RefreshToken">The refresh token used to renew the session.</param>
/// <param name="UserId">The AppManager user id — TrStudio's ownership reference.</param>
/// <param name="Role">The application role resolved for the signed-in user.</param>
/// <param name="DefaultLandingPath">The setup-gate-aware landing path chosen at sign-in.</param>
/// <param name="Email">The sign-in email, kept only to identify the session in the shell chip
/// (the login API returns no profile fields; optional so persisted pre-Email sessions and the
/// dev seams deserialize/compile unchanged).</param>
public sealed record AuthSessionState(
    string Token,
    string RefreshToken,
    string UserId,
    UserRole Role,
    string DefaultLandingPath,
    string? Email = null);
