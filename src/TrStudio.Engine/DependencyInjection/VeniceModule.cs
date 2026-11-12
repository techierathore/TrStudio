using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrStudio.Engine.Domain.Venice;
using TrStudio.Engine.Providers;
using TrStudio.Models.Providers;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>
/// Registers the Venice cloud provider (phase 2, ADR-014). Reads NOTHING from configuration: the
/// named HttpClient has no base address and no key — both come from the database per call (BRD-98).
/// </summary>
public static class VeniceModule
{
    /// <summary>Adds the Venice client, connection reader, asset IO and provider.</summary>
    /// <param name="aServices">The service collection.</param>
    /// <param name="aConfiguration">Unused; kept for the module signature every module shares.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddVeniceModule(this IServiceCollection aServices, IConfiguration aConfiguration)
    {
        aServices.AddHttpClient(VeniceClient.HttpClientName, aClient => aClient.Timeout = TimeSpan.FromMinutes(5));
        aServices.TryAddSingleton<VeniceClient>();
        aServices.TryAddSingleton<IVeniceConnectionReader, VeniceConnectionReader>();
        aServices.TryAddSingleton<VeniceAssetIo>();

        // Singleton: keeps in-memory execution state between polls, like the other providers. Its
        // Supports() claims only ProviderKind.Venice rows, so ComfyUi rows still resolve to ComfyUiProvider.
        aServices.AddSingleton<IGenerationProvider, VeniceProvider>();
        return aServices;
    }
}
