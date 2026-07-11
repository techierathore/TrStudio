using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrStudio.Engine.Domain.Comfy;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Engine.Providers;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>
/// DI registrations for the ComfyUI pipeline cluster (Phase 6, FN-043..050/BRD-34..41): the shared
/// options, the pure domain components (parser, mapper, dependency scanner, graph builder), the
/// workflow repository, the pipeline service and the <see cref="ComfyUiProvider"/> registered
/// alongside the other generation providers. Kept in its own module so the registry/spine modules
/// stay untouched.
/// </summary>
public static class ComfyModule
{
    /// <summary>
    /// Registers the ComfyUI options, domain components, repository, pipeline service and provider.
    /// </summary>
    /// <param name="aServices">The service collection to register into.</param>
    /// <param name="aConfiguration">The application configuration (section <c>ComfyUI</c>).</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddComfyModule(this IServiceCollection aServices, IConfiguration aConfiguration)
    {
        // Shared options (base URL + sandbox synthetic-fallback), registered as a singleton.
        aServices.AddSingleton(ComfyUiOptions.FromConfiguration(aConfiguration));

        // Named HTTP client for ComfyUI REST calls (/prompt, /history, /view).
        aServices.AddHttpClient(ComfyUiProvider.HttpClientName);

        // Pure domain components (stateless).
        aServices.TryAddSingleton<ComfyWorkflowParser>();
        aServices.TryAddSingleton<ComfyParamMapper>();
        aServices.TryAddSingleton<ComfyDependencyScanner>();
        aServices.TryAddSingleton<ComfyGraphBuilder>();

        // Data access + pipeline service.
        aServices.TryAddSingleton<IComfyWorkflowRepository, ComfyWorkflowRepository>();
        aServices.TryAddSingleton<IComfyPipelineService, ComfyPipelineService>();

        // Registered alongside the other providers; Supports() gates it to ProviderKind.ComfyUi so
        // the resolver still matches exactly one provider per model.
        aServices.AddSingleton<IGenerationProvider, ComfyUiProvider>();
        return aServices;
    }
}
