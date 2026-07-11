using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrStudio.Engine.Domain.Cost;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>
/// DI registrations for the cost guardrails (REQ-FN-029/BRD-13, NFR-007): the per-job
/// <see cref="ICostEstimator"/>, the spend ledger + monthly-cap service and the spend-record
/// repository. Kept in its own module so the spine/registry cluster modules stay untouched.
/// </summary>
public static class CostModule
{
    /// <summary>
    /// Registers <see cref="CostOptions"/>, the Dapper spend repository, the cost estimator and
    /// the spend ledger.
    /// </summary>
    /// <param name="aServices">The service collection to register into.</param>
    /// <param name="aConfiguration">The application configuration (section <c>Cost</c>).</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddCostModule(this IServiceCollection aServices, IConfiguration aConfiguration)
    {
        aServices.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        aServices.AddSingleton(CostOptions.FromConfiguration(aConfiguration));
        aServices.TryAddSingleton<ISpendRepository, SpendRepository>();
        aServices.TryAddSingleton<ICostEstimator, CostEstimator>();
        aServices.TryAddSingleton<ISpendLedger, SpendLedger>();
        return aServices;
    }
}
