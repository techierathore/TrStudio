using TrStudio.Models.Entities;
using TrStudio.Models.Providers;

namespace TrStudio.Engine.Domain.Providers;

/// <summary>
/// Resolves a registry model to exactly one <see cref="IGenerationProvider"/> (REQ-FN-001,
/// Architecture §5). The caller/UI never learns which provider ran the job.
/// </summary>
public interface IProviderResolver
{
    /// <summary>
    /// Resolves the single provider that supports the model's task/provider-kind pair.
    /// </summary>
    /// <param name="aModel">The resolved registry model.</param>
    /// <returns>The one provider that executes this model's jobs.</returns>
    /// <exception cref="System.InvalidOperationException">Zero or more than one registered provider supports the model.</exception>
    IGenerationProvider Resolve(RegistryModel aModel);
}
