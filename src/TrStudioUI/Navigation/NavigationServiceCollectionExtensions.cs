using Microsoft.Extensions.DependencyInjection;
using TrStudioUI.Auth;

namespace TrStudioUI.Navigation;

/// <summary>
/// DI registration for the shared, role-based app-shell navigation (REQ-UI-005 / REQ-UI-080).
/// </summary>
public static class NavigationServiceCollectionExtensions
{
    /// <summary>
    /// Registers both navigation surfaces (creator + management) and the circuit-scoped
    /// <see cref="AppShellNavigation"/> that selects the active one from the signed-in user's role.
    /// The unified TrStudio app calls this once; the shell (MainLayout / AppSidebar) then renders
    /// the surface the role is entitled to (ADR-012).
    /// </summary>
    /// <param name="aServices">The service collection being configured.</param>
    /// <param name="aCreatorNav">The creator-studios navigation surface.</param>
    /// <param name="aManagementNav">The management navigation surface.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddTrStudioRoleBasedNavigation(
        this IServiceCollection aServices,
        AppNavigationConfig aCreatorNav,
        AppNavigationConfig aManagementNav)
    {
        ArgumentNullException.ThrowIfNull(aServices);
        ArgumentNullException.ThrowIfNull(aCreatorNav);
        ArgumentNullException.ThrowIfNull(aManagementNav);

        aServices.AddScoped(aProvider => new AppShellNavigation(
            aCreatorNav,
            aManagementNav,
            aProvider.GetRequiredService<IAuthSession>()));
        return aServices;
    }
}
