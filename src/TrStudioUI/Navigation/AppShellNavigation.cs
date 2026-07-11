using TrStudio.Models.Enums;
using TrStudioUI.Auth;

namespace TrStudioUI.Navigation;

/// <summary>
/// Circuit-scoped shell state that drives the unified app's role-based surface (ADR-012 /
/// REQ-UI-080 / REQ-FN-072). It holds both navigation surfaces (creator + management), resolves
/// the signed-in user's <see cref="UserRole"/> from <see cref="IAuthSession"/>, exposes the
/// currently-active <see cref="AppNavigationConfig"/>, and answers the client-side authorization
/// question "may this user reach a management route?" that gates the <c>/admin/*</c> pages
/// (the API also enforces the admin policy server-side; this is the in-app bounce).
/// </summary>
/// <remarks>
/// Role → surface mapping: Creator sees only the creator studios; Operator only the management
/// surface; Owner sees both and may switch between them in-app. The client gate never weakens the
/// server: management API calls still require the AppManager admin role on TrStudioApi.
/// </remarks>
public sealed class AppShellNavigation
{
    private readonly AppNavigationConfig objCreatorNav;
    private readonly AppNavigationConfig objManagementNav;
    private readonly IAuthSession objAuthSession;
    private bool objInitialized;

    /// <summary>Initialises the shell with both surfaces and the auth session it reads the role from.</summary>
    /// <param name="aCreatorNav">The creator-studios navigation surface.</param>
    /// <param name="aManagementNav">The management navigation surface.</param>
    /// <param name="aAuthSession">The circuit's authentication session.</param>
    public AppShellNavigation(
        AppNavigationConfig aCreatorNav,
        AppNavigationConfig aManagementNav,
        IAuthSession aAuthSession)
    {
        objCreatorNav = aCreatorNav;
        objManagementNav = aManagementNav;
        objAuthSession = aAuthSession;
    }

    /// <summary>Raised when the resolved role or the active surface changes, so the shell re-renders.</summary>
    public event Action? Changed;

    /// <summary>The signed-in user's role, or null before initialisation / when signed out.</summary>
    public UserRole? Role { get; private set; }

    /// <summary>The surface currently shown in the shell.</summary>
    public AppSurface Surface { get; private set; } = AppSurface.Creator;

    /// <summary>Whether the user may reach the creator studios (Owner or Creator).</summary>
    public bool CanAccessCreator => Role is UserRole.Owner or UserRole.Creator;

    /// <summary>Whether the user may reach the management surface (Owner or Operator) — the <c>/admin/*</c> gate.</summary>
    public bool CanAccessManagement => Role is UserRole.Owner or UserRole.Operator;

    /// <summary>Whether the user may switch surfaces in-app (Owner only — the sole role with both).</summary>
    public bool CanSwitchSurface => CanAccessCreator && CanAccessManagement;

    /// <summary>The navigation configuration for the active surface.</summary>
    public AppNavigationConfig Current =>
        Surface == AppSurface.Management ? objManagementNav : objCreatorNav;

    /// <summary>The default landing route for the active surface.</summary>
    public string CurrentLandingPath =>
        Surface == AppSurface.Management
            ? TrStudioNavSets.ManagementLandingPath
            : TrStudioNavSets.CreatorLandingPath;

    /// <summary>The label for the in-app surface-switch control (shown for Owner only).</summary>
    public string SwitchLabel =>
        Surface == AppSurface.Management ? "Switch to Creator studio" : "Switch to Management";

    /// <summary>
    /// Resolves the signed-in user's role from the auth session and picks the default surface. The
    /// current route is honoured first (an Owner refreshing on an <c>/admin/*</c> route stays on the
    /// management surface), then the role default (Operator → management; Creator/Owner → creator).
    /// Idempotent unless <paramref name="aForceReload"/> is set, so it is safe to call from every
    /// first render.
    /// </summary>
    /// <param name="aCurrentPath">The base-relative path of the current route (may be null/empty).</param>
    /// <param name="aForceReload">Re-read the role even if already initialised (e.g. after sign-in).</param>
    public async Task InitializeAsync(string? aCurrentPath = null, bool aForceReload = false)
    {
        if (objInitialized && !aForceReload)
        {
            return;
        }

        var vState = await objAuthSession.GetAsync();
        Role = vState?.Role;
        Surface = ResolveDefaultSurface(aCurrentPath);
        objInitialized = true;
        Changed?.Invoke();
    }

    /// <summary>Picks the surface to show on (re)initialisation: current admin route wins, else the role default.</summary>
    /// <param name="aCurrentPath">The base-relative path of the current route.</param>
    /// <returns>The surface the shell should open on.</returns>
    private AppSurface ResolveDefaultSurface(string? aCurrentPath)
    {
        if (!string.IsNullOrEmpty(aCurrentPath) && IsManagementPath(aCurrentPath) && CanAccessManagement)
        {
            return AppSurface.Management;
        }

        return Role == UserRole.Operator ? AppSurface.Management : AppSurface.Creator;
    }

    /// <summary>
    /// Switches the active surface (Owner only). A no-op for any role that cannot access both
    /// surfaces, so it can never be used to escalate into a surface the role is not entitled to.
    /// </summary>
    public void SwitchSurface()
    {
        if (!CanSwitchSurface)
        {
            return;
        }

        Surface = Surface == AppSurface.Creator ? AppSurface.Management : AppSurface.Creator;
        Changed?.Invoke();
    }

    /// <summary>
    /// Client-side authorization check for the current route (REQ-FN-072): returns false when the
    /// path targets a management (<c>/admin/*</c>) route the signed-in role may not access, so the
    /// shell shows an in-app denial instead of the page. Non-management routes are always allowed
    /// here; the API remains the server-side authority for every management call.
    /// </summary>
    /// <param name="aRelativePath">The base-relative path from <c>NavigationManager.ToBaseRelativePath</c>.</param>
    /// <returns>True when the route may render for the signed-in role.</returns>
    public bool IsRouteAllowed(string aRelativePath)
    {
        if (!IsManagementPath(aRelativePath))
        {
            return true;
        }

        // Deny only once the role is known and it is not management-entitled; while the role is
        // still hydrating we let the page mount (the API still enforces the admin policy).
        return Role is null || CanAccessManagement;
    }

    /// <summary>Whether a base-relative path targets the management (<c>/admin/*</c>) surface.</summary>
    /// <param name="aRelativePath">The base-relative path (no leading slash, may carry query/hash).</param>
    /// <returns>True for an admin route.</returns>
    public static bool IsManagementPath(string aRelativePath)
    {
        var vPath = (aRelativePath ?? string.Empty).TrimStart('/');
        var vQueryIndex = vPath.IndexOfAny(['?', '#']);
        if (vQueryIndex >= 0)
        {
            vPath = vPath[..vQueryIndex];
        }

        return vPath.Equals("admin", StringComparison.OrdinalIgnoreCase)
            || vPath.StartsWith("admin/", StringComparison.OrdinalIgnoreCase);
    }
}
