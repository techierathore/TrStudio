using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Domain.Registry;

/// <summary>
/// Default <see cref="IModelCatalogService"/>: joins the registry with the provider-key store
/// so a disabled provider hides its models from the creator-facing list (REQ-FN-007), while
/// providers with no stored key (local execution) stay visible.
/// </summary>
public class ModelCatalogService : IModelCatalogService
{
    private readonly IRegistryRepository objRegistryRepository;
    private readonly IProviderKeyRepository objProviderKeyRepository;

    /// <summary>
    /// Initialises the catalog service.
    /// </summary>
    /// <param name="aRegistryRepository">The model registry data access.</param>
    /// <param name="aProviderKeyRepository">The provider key metadata data access.</param>
    public ModelCatalogService(IRegistryRepository aRegistryRepository, IProviderKeyRepository aProviderKeyRepository)
    {
        objRegistryRepository = aRegistryRepository;
        objProviderKeyRepository = aProviderKeyRepository;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RegistryModel>> ListVisibleAsync(CancellationToken aCancellationToken = default)
    {
        var vModels = await objRegistryRepository.ListAsync(true, aCancellationToken).ConfigureAwait(false);
        var vDisabledProviders = await GetDisabledProvidersAsync(aCancellationToken).ConfigureAwait(false);
        // REQ-FN-074: a text-chat LLM row is served synchronously by PromptChatService and is never a
        // generation model — keep it out of the generation studios' picker so no studio can pick it.
        return vModels
            .Where(aModel => aModel.Task != GenerationTaskType.TextChat)
            .Where(aModel => !vDisabledProviders.Contains(aModel.ProviderKind.ToString()))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<RegistryModel?> GetVisibleAsync(string aModelId, CancellationToken aCancellationToken = default)
    {
        var vModel = await objRegistryRepository.GetAsync(aModelId, aCancellationToken).ConfigureAwait(false);
        if (vModel is null || !vModel.IsEnabled)
        {
            return null;
        }

        var vDisabledProviders = await GetDisabledProvidersAsync(aCancellationToken).ConfigureAwait(false);
        return vDisabledProviders.Contains(vModel.ProviderKind.ToString()) ? null : vModel;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RegistryModel>> ListTextChatAsync(CancellationToken aCancellationToken = default)
    {
        var vModels = await objRegistryRepository.ListAsync(true, aCancellationToken).ConfigureAwait(false);
        return vModels.Where(aModel => aModel.Task == GenerationTaskType.TextChat).ToList();
    }

    private async Task<HashSet<string>> GetDisabledProvidersAsync(CancellationToken aCancellationToken)
    {
        var vKeys = await objProviderKeyRepository.ListAsync(aCancellationToken).ConfigureAwait(false);
        return vKeys.Where(aKey => !aKey.IsEnabled)
            .Select(aKey => aKey.ProviderName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
