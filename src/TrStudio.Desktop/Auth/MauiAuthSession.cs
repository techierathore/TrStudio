using System.Text.Json;
using Microsoft.Maui.Storage;
using TrStudio.Models.Contracts;
using TrStudioUI.Auth;

namespace TrStudio.Desktop.Auth;

/// <summary>
/// MAUI Blazor Hybrid implementation of <see cref="IAuthSession"/> backed by the platform
/// <see cref="SecureStorage"/> (DPAPI on Windows, Keychain on macOS) instead of the web head's
/// Blazor-Server <c>ProtectedSessionStorage</c> — which is circuit-only and does not exist in a
/// Hybrid WebView. The session (token + resolved role/claims) is cached in memory for the process
/// and persisted encrypted at rest; the token is never logged nor placed in a URL (NFR-05).
/// </summary>
/// <remarks>
/// Registered as a <b>singleton</b> in <c>MauiProgram</c>: a desktop app is single-user, and the
/// singleton is the seam that lets both the WebView-scoped components AND the
/// <see cref="MauiAuthTokenHandler"/> (created on the <c>IHttpClientFactory</c> handler scope, a
/// different scope) observe the same signed-in token — the web head solved that cross-scope problem
/// with a circuit-services accessor, which has no Hybrid analogue.
/// </remarks>
public sealed class MauiAuthSession : IAuthSession
{
    /// <summary>The SecureStorage key under which the serialized session state is persisted.</summary>
    private const string StorageKey = "trstudio.auth";

    private readonly SemaphoreSlim objGate = new(1, 1);
    private AuthSessionState? objState;
    private bool objHydrated;

    /// <summary>
    /// Synchronously seeds the in-memory session (no SecureStorage write). Used by the DEBUG dev
    /// seam to establish a role BEFORE the WebView's first render, so the role-based shell initialises
    /// on the correct role without a page reload. A no-op in Release paths (never called there).
    /// </summary>
    /// <param name="aState">The session state to seed.</param>
    public void Seed(AuthSessionState aState)
    {
        objState = aState;
        objHydrated = true;
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
            await HydrateAsync().ConfigureAwait(false);
        }

        return objState;
    }

    /// <inheritdoc />
    public async Task<string?> GetTokenAsync()
    {
        var vState = await GetAsync().ConfigureAwait(false);
        return string.IsNullOrEmpty(vState?.Token) ? null : vState.Token;
    }

    /// <inheritdoc />
    public async Task<bool> EnsureLoadedAsync()
    {
        var vState = await GetAsync().ConfigureAwait(false);
        return vState is not null;
    }

    /// <inheritdoc />
    public async Task SetAsync(LoginResponse aSession, string? aEmail = null)
    {
        ArgumentNullException.ThrowIfNull(aSession);

        objState = new AuthSessionState(
            aSession.Token,
            aSession.RefreshToken,
            aSession.UserId,
            aSession.Role,
            aSession.DefaultLandingPath,
            aEmail);
        objHydrated = true;

        await objGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var vJson = JsonSerializer.Serialize(objState);
            await SecureStorage.Default.SetAsync(StorageKey, vJson).ConfigureAwait(false);
        }
        catch
        {
            // SecureStorage can be unavailable on a bare unpackaged host; the in-memory session is
            // authoritative for the running process, so persistence is best-effort only.
        }
        finally
        {
            objGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        objState = null;
        objHydrated = true;

        await objGate.WaitAsync().ConfigureAwait(false);
        try
        {
            SecureStorage.Default.Remove(StorageKey);
        }
        catch
        {
            // Best-effort — sign-out always clears the in-memory token above.
        }
        finally
        {
            objGate.Release();
        }
    }

    /// <summary>Attempts a one-time hydrate of the session from encrypted SecureStorage.</summary>
    private async Task HydrateAsync()
    {
        await objGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (objHydrated)
            {
                return;
            }

            var vJson = await SecureStorage.Default.GetAsync(StorageKey).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(vJson))
            {
                objState = JsonSerializer.Deserialize<AuthSessionState>(vJson);
            }
        }
        catch
        {
            // Corrupt/unavailable store — treat as signed-out; the dev seam re-establishes a session.
        }
        finally
        {
            objHydrated = true;
            objGate.Release();
        }
    }
}
