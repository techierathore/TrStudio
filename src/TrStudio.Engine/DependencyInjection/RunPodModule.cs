using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrStudio.Engine.Domain.RunPod;
using TrStudio.Engine.Providers;
using TrStudio.Models.Providers;
using TrStudio.Models.Services;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>
/// DI registrations for the RunPod serverless integration (REQ-FN-028/BRD-12): the shared
/// options, the REST client seam, the admin ops service and the <see cref="RunPodProvider"/>
/// registered alongside the other generation providers. Kept in its own module so the
/// image/workspace cluster modules stay untouched.
/// </summary>
public static class RunPodModule
{
    /// <summary>
    /// Registers <see cref="RunPodOptions"/> (as a shared singleton whose idle-stop toggle is
    /// mutable at runtime), the named RunPod HTTP client + <see cref="IRunPodClient"/>, the
    /// <see cref="IRunPodOps"/> ops service and the RunPod generation provider.
    /// </summary>
    /// <param name="aServices">The service collection to register into.</param>
    /// <param name="aConfiguration">The application configuration (section <c>RunPod</c>).</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddRunPodModule(this IServiceCollection aServices, IConfiguration aConfiguration)
    {
        aServices.AddSingleton(RunPodOptions.FromConfiguration(aConfiguration));
        aServices.AddHttpClient(RunPodHttpClient.HttpClientName);
        aServices.TryAddSingleton<IRunPodClient, RunPodHttpClient>();
        aServices.TryAddSingleton<IRunPodOps, RunPodOps>();

        // Registered alongside the other providers; Supports() gates it to ProviderKind.RunPod so
        // the resolver still matches exactly one provider per model.
        aServices.AddSingleton<IGenerationProvider, RunPodProvider>();
        return aServices;
    }
}
