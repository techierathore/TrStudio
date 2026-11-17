using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TrStudio.Engine.Domain.Sequence;
using TrStudio.Engine.Infrastructure.Data;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>DI registrations for multi-shot assembly (REQ-FN-042): the store, the service and the in-app runner.</summary>
public static class SequenceModule
{
    /// <summary>Registers the sequence store, service and background runner.</summary>
    /// <param name="aServices">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSequenceModule(this IServiceCollection aServices)
    {
        aServices.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        aServices.TryAddSingleton<IClipSequenceRepository, ClipSequenceRepository>();
        aServices.TryAddSingleton<ClipSequenceRunner>();
        aServices.AddSingleton<IHostedService>(aProvider => aProvider.GetRequiredService<ClipSequenceRunner>());
        aServices.TryAddSingleton<ClipSequenceService>();
        return aServices;
    }
}
