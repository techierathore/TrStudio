using TrStudio.InProcess;
using TrStudio.Models.Contracts;
using TrStudioUI.Auth;

namespace TrStudio.Auth;

/// <summary>
/// The desktop app's <see cref="IAuthSession"/> (phase 3, ADR-019, ADR-021): the signed-in user lives
/// in memory for as long as the app is open. Only when "Remember me" was ticked does the engine keep
/// the AppManager refresh token on disk, encrypted for this user (owner decision 6B,
/// <c>RememberedSignInStore</c>); Login uses it on the next open. Signing out ends the in-process engine
/// session, after which every engine call answers 401 (REQ-FN-088), and removes what was kept.
/// </summary>
public sealed class MauiAuthSession : IAuthSession
{
    private readonly EngineHost objHost;
    private AuthSessionState? objState;

    /// <summary>Creates the session over the app's engine.</summary>
    /// <param name="aHost">The in-process engine host.</param>
    public MauiAuthSession(EngineHost aHost) => objHost = aHost;

    /// <summary>Seeds the session (the DEBUG developer session only).</summary>
    /// <param name="aState">The state.</param>
    public void Seed(AuthSessionState aState) => objState = aState;

    /// <inheritdoc />
    public Task<AuthSessionState?> GetAsync() => Task.FromResult(objState);

    /// <inheritdoc />
    public Task<string?> GetTokenAsync() => Task.FromResult(string.IsNullOrEmpty(objState?.Token) ? null : objState.Token);

    /// <inheritdoc />
    public Task<bool> EnsureLoadedAsync() => Task.FromResult(objState is not null);

    /// <inheritdoc />
    public Task SetAsync(LoginResponse aSession, string? aEmail = null)
    {
        ArgumentNullException.ThrowIfNull(aSession);
        objState = new AuthSessionState(
            aSession.Token,
            aSession.RefreshToken,
            aSession.UserId,
            aSession.Role,
            aSession.DefaultLandingPath,
            aEmail);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync()
    {
        if (!string.IsNullOrEmpty(objState?.Token))
        {
            objHost.Sessions.SignOut(objState.Token);
        }

        objHost.RememberedSignIn.Forget();

        objState = null;
        return Task.CompletedTask;
    }
}
