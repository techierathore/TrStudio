using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TrStudio.Engine.DependencyInjection;
using TrStudio.Engine.Domain.Account;
using TrStudio.Engine.Domain.ApiKeys;
using TrStudio.Engine.Domain.Compare;
using TrStudio.Engine.Domain.Export;
using TrStudio.Engine.Domain.Ops;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudioApi.Endpoints;

/// <summary>
/// Endpoint mapping for the Phase-9 polish + SaaS-seam cluster: the admin ops dashboard
/// (<c>/api/v1/admin/ops</c>, FN-064), side-by-side asset comparison (<c>/api/v1/compare</c>,
/// FN-065), the creator preset library (<c>/api/v1/presets</c>, FN-066), asset export with
/// provenance (<c>/api/v1/assets/{id}/export</c> + <c>/api/v1/assets/export</c>, FN-067) and the
/// account usage/spend + API-key surface (<c>/api/v1/account</c>, FN-068/FN-069). All owner-scoped
/// handlers resolve the caller's AppManager user id and pass it into owner-filtered services (BRD-81).
/// </summary>
public static class Phase9Endpoints
{
    private static readonly string[] PresetTypes = { "prompt", "script", "style-preset", "voice-preset" };

    /// <summary>
    /// Maps the Phase-9 endpoints onto the application pipeline.
    /// </summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapPhase9Endpoints(this WebApplication aApp)
    {
        // FN-064 — admin ops dashboard (worker health + queue depth + spend + error rate).
        aApp.MapGet(ApiRoutes.AdminOps, GetOpsDashboardAsync)
            .RequireAuthorization(SpineModule.AdminOnlyPolicy)
            .WithTags("AdminOps")
            .WithName("GetOpsDashboard");

        // FN-065 — side-by-side comparison (owner-scoped).
        aApp.MapGet(ApiRoutes.Compare, CompareAssetsAsync).WithTags("Compare").WithName("CompareAssets");

        // FN-066 — preset library CRUD (owner-scoped) + use-count bump.
        var vPresets = aApp.MapGroup(ApiRoutes.Presets).WithTags("Presets");
        vPresets.MapGet("", ListPresetsAsync).WithName("ListPresets");
        vPresets.MapPost("", CreatePresetAsync).WithName("CreatePreset");
        vPresets.MapGet("{aPresetId:guid}", GetPresetAsync).WithName("GetPreset");
        vPresets.MapPut("{aPresetId:guid}", UpdatePresetAsync).WithName("UpdatePreset");
        vPresets.MapPost("{aPresetId:guid}/use", UsePresetAsync).WithName("UsePreset");
        vPresets.MapDelete("{aPresetId:guid}", DeletePresetAsync).WithName("DeletePreset");

        // FN-067 — export single + bulk WITH provenance (owner-scoped).
        aApp.MapGet($"{ApiRoutes.Assets}/{{aAssetId:guid}}/export", ExportSingleAsync)
            .WithTags("Assets").WithName("ExportAsset");
        aApp.MapPost($"{ApiRoutes.Assets}/export", ExportBulkAsync)
            .WithTags("Assets").WithName("ExportAssetsBulk");

        // FN-068 — account usage/spend + licence (owner-scoped).
        aApp.MapGet(ApiRoutes.Account, GetAccountAsync).WithTags("Account").WithName("GetAccount");

        // FN-069 — owner-scoped API-key issue/list/revoke.
        var vKeys = aApp.MapGroup(ApiRoutes.AccountApiKeys).WithTags("Account");
        vKeys.MapGet("", ListApiKeysAsync).WithName("ListApiKeys");
        vKeys.MapPost("", IssueApiKeyAsync).WithName("IssueApiKey");
        vKeys.MapDelete("{aApiKeyId:guid}", RevokeApiKeyAsync).WithName("RevokeApiKey");

        return aApp;
    }

    // ---- FN-064 ops dashboard ----------------------------------------------------------------

    /// <summary>FN-064: returns the live ops snapshot (admin-scoped).</summary>
    private static async Task<IResult> GetOpsDashboardAsync(IOpsDashboardService aOpsService, CancellationToken aCancellationToken)
        => Results.Ok(await aOpsService.GetDashboardAsync(aCancellationToken));

    // ---- FN-065 compare ----------------------------------------------------------------------

    /// <summary>FN-065: returns each requested owned asset with its params and a computed param diff.</summary>
    private static async Task<IResult> CompareAssetsAsync(
        ClaimsPrincipal aUser,
        IAssetRepository aAssetRepository,
        CancellationToken aCancellationToken,
        [FromQuery(Name = "assetIds")] string? aAssetIds = null)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vIds = ParseGuidCsv(aAssetIds);
        if (vIds.Count < 2)
        {
            return Results.BadRequest(new { error = "Provide at least two comma-separated assetIds to compare." });
        }

        var vInputs = new List<(AssetResponse Asset, string? ParamsJson)>();
        foreach (var vId in vIds)
        {
            // Owner-filtered read: an unowned/absent id is skipped, never leaked (BRD-81).
            var vAsset = await aAssetRepository.GetAsync(vId, vUserId, aCancellationToken).ConfigureAwait(false);
            if (vAsset is not null)
            {
                vInputs.Add((ToAssetResponse(vAsset), vAsset.ParamsJson));
            }
        }

        if (vInputs.Count < 2)
        {
            return Results.BadRequest(new { error = "Fewer than two of the requested assets were found for this account." });
        }

        return Results.Ok(AssetComparer.Compare(vInputs));
    }

    // ---- FN-066 presets ----------------------------------------------------------------------

    private static async Task<IResult> ListPresetsAsync(
        ClaimsPrincipal aUser,
        IPresetRepository aPresetRepository,
        CancellationToken aCancellationToken,
        [FromQuery(Name = "type")] string? aType = null)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vPresets = await aPresetRepository.ListAsync(vUserId, NormalizeType(aType), aCancellationToken).ConfigureAwait(false);
        return Results.Ok(vPresets.Select(ToPresetResponse).ToList());
    }

    private static async Task<IResult> CreatePresetAsync(
        PresetUpsertRequest aRequest,
        ClaimsPrincipal aUser,
        IPresetRepository aPresetRepository,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(aRequest.Name))
        {
            return Results.BadRequest(new { error = "Preset name is required." });
        }

        var vType = NormalizeType(aRequest.Type);
        if (vType is null || !PresetTypes.Contains(vType))
        {
            return Results.BadRequest(new { error = $"Preset type must be one of: {string.Join(", ", PresetTypes)}." });
        }

        var vNow = DateTime.UtcNow;
        var vPreset = new Preset
        {
            PresetId = Guid.NewGuid(),
            UserId = vUserId,
            Type = vType,
            Name = aRequest.Name.Trim(),
            Description = aRequest.Description,
            Content = aRequest.Content ?? string.Empty,
            UseCount = 0,
            CreatedUtc = vNow,
            UpdatedUtc = vNow
        };

        await aPresetRepository.InsertAsync(vPreset, aCancellationToken).ConfigureAwait(false);
        return Results.Created($"{ApiRoutes.Presets}/{vPreset.PresetId}", ToPresetResponse(vPreset));
    }

    private static async Task<IResult> GetPresetAsync(
        Guid aPresetId,
        ClaimsPrincipal aUser,
        IPresetRepository aPresetRepository,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vPreset = await aPresetRepository.GetAsync(aPresetId, vUserId, aCancellationToken).ConfigureAwait(false);
        return vPreset is null ? Results.NotFound() : Results.Ok(ToPresetResponse(vPreset));
    }

    private static async Task<IResult> UpdatePresetAsync(
        Guid aPresetId,
        PresetUpsertRequest aRequest,
        ClaimsPrincipal aUser,
        IPresetRepository aPresetRepository,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vExisting = await aPresetRepository.GetAsync(aPresetId, vUserId, aCancellationToken).ConfigureAwait(false);
        if (vExisting is null)
        {
            return Results.NotFound();
        }

        if (string.IsNullOrWhiteSpace(aRequest.Name))
        {
            return Results.BadRequest(new { error = "Preset name is required." });
        }

        vExisting.Name = aRequest.Name.Trim();
        vExisting.Description = aRequest.Description;
        vExisting.Content = aRequest.Content ?? string.Empty;
        vExisting.UpdatedUtc = DateTime.UtcNow;

        await aPresetRepository.UpdateAsync(vExisting, vUserId, aCancellationToken).ConfigureAwait(false);
        return Results.Ok(ToPresetResponse(vExisting));
    }

    private static async Task<IResult> UsePresetAsync(
        Guid aPresetId,
        ClaimsPrincipal aUser,
        IPresetRepository aPresetRepository,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vPreset = await aPresetRepository.IncrementUseAsync(aPresetId, vUserId, aCancellationToken).ConfigureAwait(false);
        return vPreset is null ? Results.NotFound() : Results.Ok(ToPresetResponse(vPreset));
    }

    private static async Task<IResult> DeletePresetAsync(
        Guid aPresetId,
        ClaimsPrincipal aUser,
        IPresetRepository aPresetRepository,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vExisting = await aPresetRepository.GetAsync(aPresetId, vUserId, aCancellationToken).ConfigureAwait(false);
        if (vExisting is null)
        {
            return Results.NotFound();
        }

        await aPresetRepository.DeleteAsync(aPresetId, vUserId, aCancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    // ---- FN-067 export -----------------------------------------------------------------------

    private static async Task<IResult> ExportSingleAsync(
        Guid aAssetId,
        ClaimsPrincipal aUser,
        HttpContext aHttpContext,
        IAssetExportService aExportService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vExport = await aExportService.ExportSingleAsync(aAssetId, vUserId, aCancellationToken).ConfigureAwait(false);
        return vExport is null ? Results.NotFound() : ToFileResult(vExport, aHttpContext);
    }

    private static async Task<IResult> ExportBulkAsync(
        AssetBulkExportRequest aRequest,
        ClaimsPrincipal aUser,
        HttpContext aHttpContext,
        IAssetExportService aExportService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        if (aRequest.AssetIds is null || aRequest.AssetIds.Count == 0)
        {
            return Results.BadRequest(new { error = "Provide at least one assetId to export." });
        }

        var vExport = await aExportService.ExportBulkAsync(aRequest.AssetIds, vUserId, aCancellationToken).ConfigureAwait(false);
        return ToFileResult(vExport, aHttpContext);
    }

    // ---- FN-068 account ----------------------------------------------------------------------

    private static async Task<IResult> GetAccountAsync(
        ClaimsPrincipal aUser,
        HttpContext aHttpContext,
        IAccountService aAccountService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vAccessToken = ExtractBearer(aHttpContext);
        var vSummary = await aAccountService.GetSummaryAsync(vUserId, vAccessToken, aCancellationToken).ConfigureAwait(false);
        return Results.Ok(vSummary);
    }

    // ---- FN-069 API keys ---------------------------------------------------------------------

    private static async Task<IResult> ListApiKeysAsync(
        ClaimsPrincipal aUser,
        IApiKeyService aApiKeyService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vKeys = await aApiKeyService.ListAsync(vUserId, aCancellationToken).ConfigureAwait(false);
        return Results.Ok(vKeys);
    }

    private static async Task<IResult> IssueApiKeyAsync(
        ApiKeyIssueRequest aRequest,
        ClaimsPrincipal aUser,
        IApiKeyService aApiKeyService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(aRequest.Name))
        {
            return Results.BadRequest(new { error = "A key name is required." });
        }

        var vIssued = await aApiKeyService.IssueAsync(vUserId, aRequest.Name, aRequest.Scope, aCancellationToken).ConfigureAwait(false);
        return Results.Created($"{ApiRoutes.AccountApiKeys}/{vIssued.Key.ApiKeyId}", vIssued);
    }

    private static async Task<IResult> RevokeApiKeyAsync(
        Guid aApiKeyId,
        ClaimsPrincipal aUser,
        IApiKeyService aApiKeyService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vRevoked = await aApiKeyService.RevokeAsync(aApiKeyId, vUserId, aCancellationToken).ConfigureAwait(false);
        return vRevoked ? Results.Ok(new { revoked = true }) : Results.NotFound();
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static IResult ToFileResult(AssetExportResult aExport, HttpContext aHttpContext)
    {
        // Surface the packaged counts as response headers so callers (and the smoke) can assert the
        // export delivered every selected asset with provenance intact.
        aHttpContext.Response.Headers["X-Asset-Count"] = aExport.AssetCount.ToString();
        aHttpContext.Response.Headers["X-Provenance-Count"] = aExport.ProvenanceCount.ToString();
        return Results.File(aExport.ZipBytes, "application/zip", aExport.FileName);
    }

    private static List<Guid> ParseGuidCsv(string? aCsv)
    {
        if (string.IsNullOrWhiteSpace(aCsv))
        {
            return new List<Guid>();
        }

        var vResult = new List<Guid>();
        foreach (var vPart in aCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(vPart, out var vId) && !vResult.Contains(vId))
            {
                vResult.Add(vId);
            }
        }

        return vResult;
    }

    private static string? NormalizeType(string? aType)
        => string.IsNullOrWhiteSpace(aType) ? null : aType.Trim().ToLowerInvariant();

    private static string? ExtractBearer(HttpContext aHttpContext)
    {
        var vHeader = aHttpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(vHeader) || !vHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var vToken = vHeader["Bearer ".Length..].Trim();
        return string.IsNullOrWhiteSpace(vToken) ? null : vToken;
    }

    private static PresetResponse ToPresetResponse(Preset aPreset) => new()
    {
        PresetId = aPreset.PresetId,
        Type = aPreset.Type,
        Name = aPreset.Name,
        Description = aPreset.Description,
        Content = aPreset.Content,
        UseCount = aPreset.UseCount,
        CreatedUtc = aPreset.CreatedUtc,
        UpdatedUtc = aPreset.UpdatedUtc
    };

    private static AssetResponse ToAssetResponse(GeneratedAsset aAsset) => new()
    {
        AssetId = aAsset.AssetId,
        JobId = aAsset.JobId,
        Kind = aAsset.Kind,
        StoragePath = aAsset.StoragePath,
        ThumbnailPath = aAsset.ThumbnailPath,
        Width = aAsset.Width,
        Height = aAsset.Height,
        ModelId = aAsset.ModelId,
        ProjectId = aAsset.ProjectId,
        SeedUsed = aAsset.SeedUsed,
        CreatedUtc = aAsset.CreatedUtc
    };

    private static string? ResolveUserId(ClaimsPrincipal aUser)
        => aUser.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? aUser.FindFirst("sub")?.Value;
}
