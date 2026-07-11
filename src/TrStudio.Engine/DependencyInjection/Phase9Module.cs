using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrStudio.Engine.Domain.Account;
using TrStudio.Engine.Domain.ApiKeys;
using TrStudio.Engine.Domain.Export;
using TrStudio.Engine.Domain.Ops;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Engine.Infrastructure.Observability;
using TrStudio.Engine.Infrastructure.Ops;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>
/// DI registrations for the Phase-9 polish + SaaS-seam cluster: the ops dashboard (FN-064), asset
/// comparison support (FN-065), the preset library (FN-066), asset export with provenance
/// (FN-067), the account usage/spend summary (FN-068), external API keys (FN-069) and the
/// observability metric gauges (NFR-008). Kept in its own module so the other clusters' modules
/// stay untouched.
/// </summary>
public static class Phase9Module
{
    /// <summary>
    /// Registers the Phase-9 repositories, domain services and the observability metric gauges.
    /// </summary>
    /// <param name="aServices">The service collection to register into.</param>
    /// <param name="aConfiguration">The application configuration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddPhase9Module(this IServiceCollection aServices, IConfiguration aConfiguration)
    {
        // Shared data-access plumbing (TryAdd: the spine normally registers it first).
        aServices.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();

        // FN-066 preset library + FN-069 API keys (owner-filtered repositories, BRD-81).
        aServices.TryAddSingleton<IPresetRepository, PresetRepository>();
        aServices.TryAddSingleton<IApiKeyRepository, ApiKeyRepository>();
        aServices.TryAddSingleton<IApiKeyService, ApiKeyService>();

        // FN-064 ops dashboard: queue/worker snapshot (Hangfire) + dashboard assembly.
        aServices.TryAddSingleton<IJobQueueMonitor, HangfireJobQueueMonitor>();
        aServices.TryAddSingleton<IOpsDashboardService, OpsDashboardService>();

        // FN-067 export with provenance + FN-068 account usage/spend summary.
        aServices.TryAddSingleton<IAssetExportService, AssetExportService>();
        aServices.TryAddSingleton<IAccountService, AccountService>();

        // NFR-008: the observable queue-depth + spend gauges live for the host lifetime.
        aServices.AddHostedService<TrStudioMetricsRegistration>();

        return aServices;
    }
}
