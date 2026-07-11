using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrStudio.Engine.Domain.Install;
using TrStudio.Engine.Domain.ModelSources;
using TrStudio.Engine.Domain.Setup;
using TrStudio.Engine.Infrastructure.Capability;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Engine.Infrastructure.Install;
using TrStudio.Engine.Infrastructure.ModelSources;
using TrStudio.Engine.Infrastructure.Setup;
using TrStudio.Engine.Workers.Install;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>
/// DI registrations for the setup cluster: capability detection, first-run environment setup,
/// model sources and model installs.
/// </summary>
/// <remarks>
/// OWNED BY: cluster C (setup/sources/installs, scripts 0300-0399).
/// Only that cluster edits this file; other clusters register services in their own module.
/// </remarks>
public static class SetupModule
{
    /// <summary>
    /// Registers the setup services: <c>ICapabilityDetector</c> (REQ-FN-008), the guided-setup
    /// step engine <c>ISetupService</c>/<c>ISetupStepEngine</c> (REQ-FN-009/010), the model
    /// source repository + encrypted token store (REQ-FN-011 groundwork) and the resumable
    /// model install service <c>IModelInstallService</c> (REQ-FN-011).
    /// </summary>
    /// <param name="aServices">The service collection to register into.</param>
    /// <param name="aConfiguration">The application configuration (section <c>Setup</c>).</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddSetupModule(this IServiceCollection aServices, IConfiguration aConfiguration)
    {
        var vOptions = SetupOptions.FromConfiguration(aConfiguration);
        aServices.AddSingleton(vOptions);
        aServices.AddSingleton(new SetupPaths(vOptions));

        aServices.AddMemoryCache();
        aServices.AddHttpClient(InstallDownloader.HttpClientName, aClient => aClient.Timeout = Timeout.InfiniteTimeSpan);
        aServices.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();

        // REQ-FN-008 — capability detection (cached in-process, short TTL).
        aServices.AddSingleton<ICapabilityDetector, CapabilityDetector>();

        // REQ-FN-009/010 — guided-setup step engine over the managed environment root.
        aServices.AddSingleton<SetupProcessRunner>();
        aServices.AddSingleton<ISetupStateRepository, SetupStateRepository>();
        aServices.AddSingleton<ISetupStepHandler, OnnxSetupStepHandler>();
        aServices.AddSingleton<ISetupStepHandler, PythonEnvSetupStepHandler>();
        aServices.AddSingleton<ISetupStepHandler, ComfyUiSetupStepHandler>();
        aServices.AddSingleton<SetupService>();
        aServices.AddSingleton<ISetupService>(aProvider => aProvider.GetRequiredService<SetupService>());
        aServices.AddSingleton<ISetupStepEngine>(aProvider => aProvider.GetRequiredService<SetupService>());

        // REQ-FN-011 groundwork — model sources with AES-GCM-encrypted tokens.
        aServices.AddSingleton<ISetupSecretProtector, SetupSecretProtector>();
        aServices.AddSingleton<ModelSourceRepository>();
        aServices.AddSingleton<IModelSourceRepository>(aProvider => aProvider.GetRequiredService<ModelSourceRepository>());
        aServices.AddSingleton<IModelSourceTokenStore>(aProvider => aProvider.GetRequiredService<ModelSourceRepository>());

        // REQ-FN-011 — persistent, resumable model install queue.
        aServices.AddSingleton<IInstallDownloader, InstallDownloader>();
        aServices.AddSingleton<InstallDestinationPlanner>();
        aServices.AddSingleton<IModelInstallRepository, ModelInstallRepository>();
        aServices.AddSingleton<ModelInstallService>();
        aServices.AddSingleton<IModelInstallService>(aProvider => aProvider.GetRequiredService<ModelInstallService>());
        aServices.AddSingleton<IInstallPreviewService>(aProvider => aProvider.GetRequiredService<ModelInstallService>());

        return aServices;
    }
}
