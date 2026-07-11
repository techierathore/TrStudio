using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>
/// DI registrations for the workspace cluster: gallery/asset browsing and creator projects.
/// </summary>
/// <remarks>
/// OWNED BY: cluster G (workspace — gallery/projects, scripts 0600-0699).
/// Only that cluster edits this file; other clusters register services in their own module.
/// </remarks>
public static class WorkspaceModule
{
    /// <summary>
    /// Registers the workspace services (<see cref="IAssetRepository"/> for the gallery and
    /// <see cref="IProjectRepository"/> for projects), all owner-filtered (REQ-FN-025/026/027).
    /// </summary>
    /// <param name="aServices">The service collection to register into.</param>
    /// <param name="aConfiguration">The application configuration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddWorkspaceModule(this IServiceCollection aServices, IConfiguration aConfiguration)
    {
        // Shared data-access + storage plumbing (TryAdd: the spine normally registers these first).
        aServices.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        aServices.TryAddSingleton<IAssetRepository, AssetRepository>();
        aServices.TryAddSingleton<IAssetStorage, LocalAssetStorage>();

        // Creator projects (owner-filtered, BRD-81).
        aServices.TryAddSingleton<IProjectRepository, ProjectRepository>();

        return aServices;
    }
}
