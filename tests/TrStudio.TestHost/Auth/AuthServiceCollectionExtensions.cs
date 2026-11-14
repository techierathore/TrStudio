using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection;

namespace TrStudioUI.Auth;

/// <summary>
/// Registration helpers wiring the shared TrStudio auth session into a Blazor Server host: the
/// circuit-scoped <see cref="IAuthSession"/>, the <see cref="CircuitServicesAccessor"/> bridge and
/// its <see cref="ServicesAccessorCircuitHandler"/>, and the <see cref="AuthTokenHandler"/> that
/// attaches the bearer token to the named <c>TrStudioApi</c> client.
/// </summary>
public static class AuthServiceCollectionExtensions
{
    /// <summary>
    /// Registers the auth session, circuit-service bridge and bearer <see cref="AuthTokenHandler"/>.
    /// Call before <c>AddHttpClient("TrStudioApi")…AddHttpMessageHandler&lt;AuthTokenHandler&gt;()</c>.
    /// </summary>
    /// <param name="aServices">The host service collection.</param>
    /// <returns>The same collection for chaining.</returns>
    public static IServiceCollection AddTrStudioAuthSession(this IServiceCollection aServices)
    {
        aServices.AddScoped<IAuthSession, AuthSession>();
        aServices.AddScoped<CircuitServicesAccessor>();
        aServices.AddScoped<CircuitHandler, ServicesAccessorCircuitHandler>();
        aServices.AddScoped<AuthTokenHandler>();
        return aServices;
    }
}
