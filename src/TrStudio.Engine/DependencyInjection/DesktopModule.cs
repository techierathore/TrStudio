using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrStudio.Engine.Infrastructure.Security;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>DI registrations the desktop shape adds (phase 3).</summary>
public static class DesktopModule
{
    /// <summary>Registers <see cref="DesktopProviderSettings"/>, which loads the user's keys from the database.</summary>
    /// <param name="aServices">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddDesktopProviderSettings(this IServiceCollection aServices)
    {
        aServices.TryAddSingleton<DesktopProviderSettings>();
        return aServices;
    }
}
