using TrStudio.Models.Entities;

namespace TrStudio.Engine.Domain.Registry;

/// <summary>
/// The creator-facing model catalog (REQ-FN-006/007): registry rows that are enabled AND whose
/// provider key (if the provider needs one) is not disabled. Disabling a provider hides all of
/// its models here without touching the registry rows.
/// </summary>
public interface IModelCatalogService
{
    /// <summary>
    /// Lists the models selectable by creators, with their declarative params schema.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The visible registry models.</returns>
    Task<IReadOnlyList<RegistryModel>> ListVisibleAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Gets a single model if it is currently visible to creators.
    /// </summary>
    /// <param name="aModelId">The registry model identifier.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The model, or null when unknown, disabled or hidden by a disabled provider.</returns>
    Task<RegistryModel?> GetVisibleAsync(string aModelId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists the enabled <see cref="TrStudio.Models.Enums.GenerationTaskType.TextChat"/> registry
    /// rows for the Prompt Studio LLM picker (REQ-FN-074 / UI-081). These are deliberately absent
    /// from <see cref="ListVisibleAsync"/> (the generation catalog); the returned rows carry the
    /// chat endpoint/model config but never an API key (there is no key field on the row).
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The enabled text-chat models.</returns>
    Task<IReadOnlyList<RegistryModel>> ListTextChatAsync(CancellationToken aCancellationToken = default);
}
