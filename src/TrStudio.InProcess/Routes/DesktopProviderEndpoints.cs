using TrStudio.Engine.DependencyInjection;
using TrStudio.Engine.Infrastructure.Security;
using TrStudio.InProcess.Hosting;
using TrStudio.Models;

namespace TrStudio.InProcess.Routes;

/// <summary>
/// The key-protection note for Registry and providers (phase 3, REQ-UI-104): where the user's keys are
/// kept and what protects them.
/// </summary>
public static class DesktopProviderEndpoints
{
    /// <summary>The route.</summary>
    public const string ProtectionRoute = ApiRoutes.Providers + "/protection";

    /// <summary>Maps the route.</summary>
    /// <param name="aApp">The route table.</param>
    /// <returns>The same table.</returns>
    public static WebApplication MapDesktopProviderEndpoints(this WebApplication aApp)
    {
        aApp.MapGet(ProtectionRoute, (IKeyProtector aProtector) => Results.Ok(new
        {
            note = $"Keys stay on this computer, in your TrStudio library, protected by this computer's own protection for your user ({aProtector.Description}).",
            protectedBy = aProtector.Description
        })).RequireAuthorization(SpineModule.AdminOnlyPolicy);
        return aApp;
    }
}
