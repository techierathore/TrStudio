using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrStudio.Engine.Domain.Jobs;
using TrStudio.Engine.Domain.Progress;
using TrStudio.Engine.Domain.Providers;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>
/// DI registrations for the spine cluster: job orchestration, Hangfire queue wiring,
/// generation log, progress publishing and the shared data-access plumbing.
/// </summary>
/// <remarks>
/// OWNED BY: cluster A (spine — jobs/queue/progress/log, scripts 0100-0199).
/// Only that cluster edits this file; other clusters register services in their own module.
/// </remarks>
public static class SpineModule
{
    /// <summary>
    /// The authorization policy name gating admin-scoped endpoints (registry CRUD, provider
    /// keys, Hangfire dashboard). Requires a <c>role</c> claim of Owner or Operator. Cluster D
    /// reuses this name — do not rename.
    /// </summary>
    public const string AdminOnlyPolicy = "AdminOnly";

    /// <summary>
    /// Registers the spine services (job repository, orchestrator, Hangfire Postgres storage +
    /// server, <c>IDbConnectionFactory</c>, progress publisher, local asset storage) and
    /// defines the <see cref="AdminOnlyPolicy"/> authorization policy (REQ-FN-002/003/004/005/018).
    /// </summary>
    /// <param name="aServices">The service collection to register into.</param>
    /// <param name="aConfiguration">The application configuration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddSpineModule(this IServiceCollection aServices, IConfiguration aConfiguration)
    {
        // Shared data-access plumbing (TryAdd: other modules may defensively register it too).
        aServices.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();

        // Spine repositories (stateless over the connection factory).
        aServices.TryAddSingleton<IJobRepository, JobRepository>();
        aServices.TryAddSingleton<IGenerationLogRepository, GenerationLogRepository>();
        aServices.TryAddSingleton<IAssetRepository, AssetRepository>();

        // Local object store for generated asset binaries (Storage:AssetsRoot).
        aServices.TryAddSingleton<IAssetStorage, LocalAssetStorage>();

        // Progress publishing seam: the worker publishes through IProgressNotifier; the API
        // host subscribes the SignalR bridge onto the same ProgressBroadcaster singleton.
        aServices.TryAddSingleton<ProgressBroadcaster>();
        aServices.TryAddSingleton<IProgressNotifier>(aProvider => aProvider.GetRequiredService<ProgressBroadcaster>());

        // Orchestration: modelId → registry row → exactly one provider (REQ-FN-001).
        aServices.TryAddSingleton<IProviderResolver, ProviderResolver>();
        aServices.TryAddSingleton<IJobOrchestrator, JobOrchestrator>();
        aServices.AddTransient<GenerationJobWorker>();

        // AdminOnly policy (REQ-FN-004): role claim Owner or Operator. Dev tokens carry the
        // resolved role directly; REAL AppManager tokens carry no app role claim at all
        // (appManagerRole + "appId:Role" appRole entries instead), so the assertion also
        // resolves via UserRoleMapper exactly like login and /me do — otherwise a real Owner
        // gets 403 on every /admin/* endpoint (found in the 2026-07-12 fix-issues UAT).
        var vAppManagerSection = aConfiguration.GetSection("AppManager");
        var vSuperAdminEmail = vAppManagerSection["SuperAdminEmail"] ?? "admin@appmanager.local";
        var vApplicationId = vAppManagerSection.GetValue("ApplicationId", 0);
        aServices.AddAuthorizationCore(aOptions =>
            aOptions.AddPolicy(AdminOnlyPolicy, aPolicy => aPolicy.RequireAssertion(aContext =>
                IsAdminPrincipal(aContext.User, vSuperAdminEmail, vApplicationId))));

        // Durable job queue: Hangfire with PostgreSQL storage — no Redis (REQ-FN-003, ADR).
        var vConnectionString = aConfiguration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
        var vInvisibilityTimeoutSeconds = aConfiguration.GetValue("Jobs:InvisibilityTimeoutSeconds", 300);
        var vWorkerCount = aConfiguration.GetValue("Jobs:WorkerCount", 2);

        aServices.AddHangfire((aProvider, aHangfire) => aHangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                aStorage => aStorage.UseNpgsqlConnection(vConnectionString),
                new PostgreSqlStorageOptions
                {
                    QueuePollInterval = TimeSpan.FromSeconds(2),
                    InvisibilityTimeout = TimeSpan.FromSeconds(vInvisibilityTimeoutSeconds)
                }));

        aServices.AddHangfireServer(aOptions =>
        {
            aOptions.Queues = new[] { "generation", "default" };
            aOptions.WorkerCount = Math.Max(1, vWorkerCount);
        });

        return aServices;
    }

    /// <summary>
    /// Admin-gate check for <see cref="AdminOnlyPolicy"/>: accepts a resolved <c>role</c> claim
    /// of Owner/Operator (dev tokens), else resolves the TrStudio role from the raw AppManager
    /// claims — superadmin email, AppManager-level role, and this application's <c>appRole</c>
    /// entry (<c>"{applicationId}:{RoleName}"</c>) — via <see cref="UserRoleMapper"/>, the same
    /// mapping login and <c>/me</c> use.
    /// </summary>
    /// <param name="aUser">The authenticated principal.</param>
    /// <param name="aSuperAdminEmail">The configured superadmin bootstrap email.</param>
    /// <param name="aApplicationId">This app's AppManager application id.</param>
    /// <returns><c>true</c> when the principal resolves to Owner or Operator.</returns>
    private static bool IsAdminPrincipal(System.Security.Claims.ClaimsPrincipal aUser, string aSuperAdminEmail, int aApplicationId)
    {
        if (aUser.Claims.Any(aClaim =>
                (aClaim.Type == "role" || aClaim.Type == System.Security.Claims.ClaimTypes.Role)
                && aClaim.Value is "Owner" or "Operator"))
        {
            return true;
        }

        var vEmail = aUser.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? aUser.FindFirst("email")?.Value;
        var vAppManagerRole = aUser.FindFirst("appManagerRole")?.Value;
        var vPrefix = aApplicationId + ":";
        var vApplicationRole = aUser.FindAll("appRole")
            .Select(aClaim => aClaim.Value)
            .FirstOrDefault(aValue => aValue.StartsWith(vPrefix, StringComparison.Ordinal))?[vPrefix.Length..]
            ?? aUser.FindFirst("applicationRole")?.Value;

        var vRole = Domain.Identity.UserRoleMapper.MapRole(vEmail, vAppManagerRole, vApplicationRole, aSuperAdminEmail);
        return vRole is TrStudio.Models.Enums.UserRole.Owner or TrStudio.Models.Enums.UserRole.Operator;
    }
}
