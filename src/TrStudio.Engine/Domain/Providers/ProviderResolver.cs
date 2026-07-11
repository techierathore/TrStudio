using TrStudio.Models.Entities;
using TrStudio.Models.Providers;

namespace TrStudio.Engine.Domain.Providers;

/// <summary>
/// Default <see cref="IProviderResolver"/>: scans the DI-registered
/// <see cref="IGenerationProvider"/> set and demands exactly one match per model, keeping the
/// registry the single routing authority (REQ-FN-001).
/// </summary>
public class ProviderResolver : IProviderResolver
{
    private readonly IReadOnlyList<IGenerationProvider> objProviders;

    /// <summary>
    /// Initialises the resolver over all registered providers.
    /// </summary>
    /// <param name="aProviders">The DI-registered provider implementations.</param>
    public ProviderResolver(IEnumerable<IGenerationProvider> aProviders)
    {
        objProviders = aProviders.ToList();
    }

    /// <inheritdoc />
    public IGenerationProvider Resolve(RegistryModel aModel)
    {
        var vMatches = objProviders.Where(aProvider => aProvider.Supports(aModel.Task, aModel)).ToList();
        if (vMatches.Count == 0)
        {
            throw new InvalidOperationException(
                $"No generation provider is registered for model '{aModel.Id}' (task {aModel.Task}, provider kind {aModel.ProviderKind}).");
        }

        if (vMatches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple generation providers claim model '{aModel.Id}'; the registry must resolve to exactly one.");
        }

        return vMatches[0];
    }
}
