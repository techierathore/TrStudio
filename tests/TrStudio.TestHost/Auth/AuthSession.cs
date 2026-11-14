using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using TrStudio.Models.Contracts;

namespace TrStudioUI.Auth;

/// <summary>
/// Default <see cref="IAuthSession"/> backed by <see cref="ProtectedSessionStorage"/> — the token
/// is encrypted with the server data-protection key, so it is never exposed as plaintext in the
/// browser store (NFR-05). The state is cached in memory for the lifetime of the circuit and
/// hydrated lazily; hydration during prerender (when JS interop is unavailable) fails softly and
/// is retried on the first interactive render.
/// </summary>
public sealed class AuthSession : IAuthSession
{
    /// <summary>The encrypted session-storage key under which the state is persisted.</summary>
    private const string StorageKey = "trstudio.auth";

    private readonly ProtectedSessionStorage objStorage;
    private AuthSessionState? objState;
    private bool objHydrated;

    /// <summary>Initialises the session over the circuit's encrypted session storage.</summary>
    /// <param name="aStorage">The data-protection-backed browser session store.</param>
    public AuthSession(ProtectedSessionStorage aStorage)
    {
        objStorage = aStorage;
    }

    /// <inheritdoc />
    public async Task<AuthSessionState?> GetAsync()
    {
        if (objState is not null)
        {
            return objState;
        }

        if (!objHydrated)
        {
            await HydrateAsync();
        }

        return objState;
    }

    /// <inheritdoc />
    public async Task<string?> GetTokenAsync()
    {
        var vState = await GetAsync();
        return string.IsNullOrEmpty(vState?.Token) ? null : vState.Token;
    }

    /// <inheritdoc />
    public async Task<bool> EnsureLoadedAsync()
    {
        var vState = await GetAsync();
        return vState is not null;
    }

    /// <inheritdoc />
    public async Task SetAsync(LoginResponse aSession, string? aEmail = null)
    {
        objState = new AuthSessionState(
            aSession.Token,
            aSession.RefreshToken,
            aSession.UserId,
            aSession.Role,
            aSession.DefaultLandingPath,
            aEmail);
        objHydrated = true;

        try
        {
            await objStorage.SetAsync(StorageKey, objState);
        }
        catch
        {
            // JS interop is unavailable during prerender; SetAsync is invoked from an interactive
            // click handler, so this only guards the rare prerender race — state stays in memory.
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        objState = null;
        objHydrated = true;

        try
        {
            await objStorage.DeleteAsync(StorageKey);
        }
        catch
        {
            // Prerender guard — see SetAsync. Sign-out always clears the in-memory token.
        }
    }

    /// <summary>Attempts a one-time hydrate from encrypted session storage.</summary>
    private async Task HydrateAsync()
    {
        try
        {
            var vResult = await objStorage.GetAsync<AuthSessionState>(StorageKey);
            if (vResult.Success)
            {
                objState = vResult.Value;
            }

            objHydrated = true;
        }
        catch
        {
            // JS interop is not available during prerender — leave objHydrated false so the first
            // interactive render retries and the token becomes available before the API call.
        }
    }
}
