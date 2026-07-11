using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrStudio.Engine.Domain.Registry;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Engine.Infrastructure.Security;
using TrStudio.Engine.Providers;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>
/// DI registrations for the registry cluster: the model/provider registry, provider key
/// management (AES-GCM key store) and the concrete generation providers.
/// </summary>
/// <remarks>
/// OWNED BY: cluster A this wave (registry/providers, scripts 0200-0299).
/// Other clusters register services in their own module.
/// </remarks>
public static class RegistryModule
{
    /// <summary>
    /// Registers the registry services (<c>IRegistryRepository</c>, <c>IProviderKeyRepository</c>,
    /// the AES-GCM <c>IKeyStore</c>, the creator-facing model catalog) and the
    /// <see cref="StubEchoProvider"/> backing the seeded <c>stub-echo</c> row
    /// (REQ-FN-001/006/007).
    /// </summary>
    /// <param name="aServices">The service collection to register into.</param>
    /// <param name="aConfiguration">The application configuration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddRegistryModule(this IServiceCollection aServices, IConfiguration aConfiguration)
    {
        // Shared data-access plumbing (TryAdd: SpineModule normally registers it first).
        aServices.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();

        // Registry + provider-key data access.
        aServices.TryAddSingleton<IRegistryRepository, RegistryRepository>();
        aServices.TryAddSingleton<IProviderKeyRepository, ProviderKeyRepository>();

        // Encrypted key store (AES-GCM, key derived from Security:KeyStoreKey — NFR-05).
        aServices.TryAddSingleton<IKeyStore, AesGcmKeyStore>();

        // Creator-facing catalog: enabled models whose provider is not disabled (REQ-FN-006/007).
        aServices.TryAddSingleton<IModelCatalogService, ModelCatalogService>();

        // Concrete providers. Singleton: the stub keeps in-memory execution state between polls.
        aServices.AddSingleton<IGenerationProvider, StubEchoProvider>();

        return aServices;
    }
}
