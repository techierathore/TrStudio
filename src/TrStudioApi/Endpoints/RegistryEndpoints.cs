using TrStudio.Engine.DependencyInjection;
using TrStudio.Engine.Domain.Registry;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudioApi.Endpoints;

/// <summary>
/// Endpoint mapping for the registry cluster: admin-scoped model/provider registry CRUD with
/// the declarative params-schema round-trip (<c>/api/v1/admin/registry</c>, REQ-FN-006),
/// admin-scoped provider key management over the encrypted key store
/// (<c>/api/v1/admin/providers</c>, REQ-FN-007 — keys are write-only, listings are masked),
/// and the creator-facing selectable-model list (<c>/api/v1/models</c>).
/// </summary>
/// <remarks>
/// OWNED BY: cluster A this wave (registry/providers). Other clusters map their endpoints in
/// their own file.
/// </remarks>
public static class RegistryEndpoints
{
    /// <summary>
    /// Maps the registry, provider-key and model-catalog endpoints onto the application pipeline.
    /// </summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapRegistryEndpoints(this WebApplication aApp)
    {
        // Creator-facing catalog: enabled models with their params schema; models of disabled
        // providers are hidden (REQ-FN-006/007). Authenticated via the fallback policy.
        aApp.MapGet(ApiRoutes.Models, ListModelsAsync).WithTags("Models").WithName("ListModels");

        // Prompt Studio picker: the registry's enabled text-chat LLM rows only (REQ-FN-074/UI-081).
        // Authenticated (creators, not admin) — same fallback policy as the generation catalog above.
        // The returned RegistryModel shape carries the endpoint/chat-model/vision flag but NEVER an API
        // key (there is no key field on the row — it lives in the encrypted key store keyed by row id).
        aApp.MapGet(ApiRoutes.TextChatModels, ListTextChatModelsAsync).WithTags("Models").WithName("ListTextChatModels");

        var vRegistry = aApp.MapGroup(ApiRoutes.Registry)
            .RequireAuthorization(SpineModule.AdminOnlyPolicy)
            .WithTags("AdminRegistry");

        vRegistry.MapGet("", ListRegistryAsync).WithName("ListRegistryModels");
        vRegistry.MapGet("{aModelId}", GetRegistryModelAsync).WithName("GetRegistryModel");
        vRegistry.MapPut("{aModelId}", UpsertRegistryModelAsync).WithName("UpsertRegistryModel");
        vRegistry.MapPost("{aModelId}/enable", (string aModelId, IRegistryRepository aRepository, CancellationToken aCt)
            => SetRegistryEnabledAsync(aModelId, true, aRepository, aCt)).WithName("EnableRegistryModel");
        vRegistry.MapPost("{aModelId}/disable", (string aModelId, IRegistryRepository aRepository, CancellationToken aCt)
            => SetRegistryEnabledAsync(aModelId, false, aRepository, aCt)).WithName("DisableRegistryModel");
        vRegistry.MapDelete("{aModelId}", DeleteRegistryModelAsync).WithName("DeleteRegistryModel");

        var vProviders = aApp.MapGroup(ApiRoutes.Providers)
            .RequireAuthorization(SpineModule.AdminOnlyPolicy)
            .WithTags("AdminProviders");

        vProviders.MapGet("keys", ListProviderKeysAsync).WithName("ListProviderKeys");
        vProviders.MapPut("keys", UpsertProviderKeyAsync).WithName("UpsertProviderKey");
        vProviders.MapPost("keys/{aProviderName}/enable", (string aProviderName, IProviderKeyRepository aRepository, CancellationToken aCt)
            => SetProviderKeyEnabledAsync(aProviderName, true, aRepository, aCt)).WithName("EnableProviderKey");
        vProviders.MapPost("keys/{aProviderName}/disable", (string aProviderName, IProviderKeyRepository aRepository, CancellationToken aCt)
            => SetProviderKeyEnabledAsync(aProviderName, false, aRepository, aCt)).WithName("DisableProviderKey");
        vProviders.MapDelete("keys/{aProviderName}", DeleteProviderKeyAsync).WithName("DeleteProviderKey");

        return aApp;
    }

    private static async Task<IResult> ListModelsAsync(IModelCatalogService aCatalog, CancellationToken aCancellationToken)
    {
        var vModels = await aCatalog.ListVisibleAsync(aCancellationToken);
        return Results.Ok(vModels);
    }

    private static async Task<IResult> ListTextChatModelsAsync(IModelCatalogService aCatalog, CancellationToken aCancellationToken)
    {
        var vModels = await aCatalog.ListTextChatAsync(aCancellationToken);
        return Results.Ok(vModels);
    }

    private static async Task<IResult> ListRegistryAsync(IRegistryRepository aRepository, CancellationToken aCancellationToken)
    {
        var vModels = await aRepository.ListAsync(false, aCancellationToken);
        return Results.Ok(vModels);
    }

    private static async Task<IResult> GetRegistryModelAsync(string aModelId, IRegistryRepository aRepository, CancellationToken aCancellationToken)
    {
        var vModel = await aRepository.GetAsync(aModelId, aCancellationToken);
        return vModel is null ? Results.NotFound() : Results.Ok(vModel);
    }

    private static async Task<IResult> UpsertRegistryModelAsync(
        string aModelId,
        RegistryModelUpsertRequest aRequest,
        IRegistryRepository aRepository,
        CancellationToken aCancellationToken)
    {
        if (string.IsNullOrWhiteSpace(aRequest.Model.Id))
        {
            aRequest.Model.Id = aModelId;
        }

        if (!string.Equals(aRequest.Model.Id, aModelId, StringComparison.Ordinal))
        {
            return Results.BadRequest(new { error = "The route model id and body model id must match." });
        }

        await aRepository.UpsertAsync(aRequest.Model, aCancellationToken);
        var vStored = await aRepository.GetAsync(aModelId, aCancellationToken);
        return Results.Ok(vStored);
    }

    private static async Task<IResult> SetRegistryEnabledAsync(
        string aModelId,
        bool aIsEnabled,
        IRegistryRepository aRepository,
        CancellationToken aCancellationToken)
    {
        var vModel = await aRepository.GetAsync(aModelId, aCancellationToken);
        if (vModel is null)
        {
            return Results.NotFound();
        }

        await aRepository.SetEnabledAsync(aModelId, aIsEnabled, aCancellationToken);
        return Results.Ok(new { id = aModelId, isEnabled = aIsEnabled });
    }

    private static async Task<IResult> DeleteRegistryModelAsync(string aModelId, IRegistryRepository aRepository, CancellationToken aCancellationToken)
    {
        var vModel = await aRepository.GetAsync(aModelId, aCancellationToken);
        if (vModel is null)
        {
            return Results.NotFound();
        }

        await aRepository.DeleteAsync(aModelId, aCancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListProviderKeysAsync(IKeyStore aKeyStore, CancellationToken aCancellationToken)
    {
        var vKeys = await aKeyStore.ListMaskedAsync(aCancellationToken);
        return Results.Ok(vKeys);
    }

    private static async Task<IResult> UpsertProviderKeyAsync(
        ProviderKeyUpsertRequest aRequest,
        IKeyStore aKeyStore,
        IProviderKeyRepository aRepository,
        CancellationToken aCancellationToken)
    {
        if (string.IsNullOrWhiteSpace(aRequest.ProviderName) || string.IsNullOrWhiteSpace(aRequest.ApiKey))
        {
            return Results.BadRequest(new { error = "ProviderName and ApiKey are required." });
        }

        // Write-only: the raw key goes into the AES-GCM key store and is never echoed back.
        await aKeyStore.StoreAsync(aRequest.ProviderName, aRequest.ApiKey, aCancellationToken);
        await aRepository.SetEnabledAsync(aRequest.ProviderName, aRequest.IsEnabled, aCancellationToken);

        var vMasked = (await aKeyStore.ListMaskedAsync(aCancellationToken))
            .FirstOrDefault(aInfo => aInfo.ProviderName == aRequest.ProviderName);
        return Results.Ok(vMasked);
    }

    private static async Task<IResult> SetProviderKeyEnabledAsync(
        string aProviderName,
        bool aIsEnabled,
        IProviderKeyRepository aRepository,
        CancellationToken aCancellationToken)
    {
        var vUpdated = await aRepository.SetEnabledAsync(aProviderName, aIsEnabled, aCancellationToken);
        return vUpdated
            ? Results.Ok(new { providerName = aProviderName, isEnabled = aIsEnabled })
            : Results.NotFound();
    }

    private static async Task<IResult> DeleteProviderKeyAsync(
        string aProviderName,
        IKeyStore aKeyStore,
        CancellationToken aCancellationToken)
    {
        await aKeyStore.DeleteAsync(aProviderName, aCancellationToken);
        return Results.NoContent();
    }
}
