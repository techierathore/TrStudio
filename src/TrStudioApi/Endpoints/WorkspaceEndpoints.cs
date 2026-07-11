using System.Security.Claims;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudioApi.Endpoints;

/// <summary>
/// Endpoint mapping for the workspace cluster: the generated-asset gallery
/// (<c>/api/v1/assets</c> — browse/filter and reuse-as-input, REQ-FN-025) and creator projects
/// (<c>/api/v1/projects</c> — CRUD plus per-project generation history and one-click reproduce,
/// REQ-FN-026). EVERY handler resolves the caller's AppManager user id and passes it into the
/// owner-filtered repositories so a user can never read another user's assets or projects
/// (BRD-81 / REQ-FN-027).
/// </summary>
/// <remarks>
/// OWNED BY: cluster G (workspace). Only that cluster edits this file; other clusters map their
/// endpoints in their own file.
/// </remarks>
public static class WorkspaceEndpoints
{
    /// <summary>
    /// Maps the workspace endpoints (assets gallery + projects) onto the application pipeline.
    /// </summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapWorkspaceEndpoints(this WebApplication aApp)
    {
        var vAssets = aApp.MapGroup(ApiRoutes.Assets).WithTags("Assets");
        vAssets.MapGet("", ListAssetsAsync).WithName("ListAssets");
        vAssets.MapGet("{aAssetId:guid}", GetAssetAsync).WithName("GetAsset");

        var vProjects = aApp.MapGroup(ApiRoutes.Projects).WithTags("Projects");
        vProjects.MapGet("", ListProjectsAsync).WithName("ListProjects");
        vProjects.MapPost("", CreateProjectAsync).WithName("CreateProject");
        vProjects.MapGet("{aProjectId:guid}", GetProjectAsync).WithName("GetProject");
        vProjects.MapPut("{aProjectId:guid}", UpdateProjectAsync).WithName("UpdateProject");
        vProjects.MapDelete("{aProjectId:guid}", ArchiveProjectAsync).WithName("ArchiveProject");
        vProjects.MapGet("{aProjectId:guid}/history", GetProjectHistoryAsync).WithName("GetProjectHistory");

        return aApp;
    }

    // ---- Assets (REQ-FN-025) --------------------------------------------------------------

    private static async Task<IResult> ListAssetsAsync(
        ClaimsPrincipal aUser,
        IAssetRepository aAssetRepository,
        CancellationToken aCancellationToken,
        string? aSearch = null,
        string? aKind = null,
        string? aModelId = null,
        Guid? aProjectId = null,
        int aPage = 1,
        int aPageSize = 50)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vFilter = new AssetFilterRequest
        {
            Search = aSearch,
            Kind = aKind,
            ModelId = aModelId,
            ProjectId = aProjectId,
            Page = aPage,
            PageSize = aPageSize
        };

        var vPage = await aAssetRepository.ListAsync(vFilter, vUserId, aCancellationToken);
        return Results.Ok(new PagedResult<AssetResponse>
        {
            Items = vPage.Items.Select(ToAssetResponse).ToList(),
            TotalCount = vPage.TotalCount,
            Page = vPage.Page,
            PageSize = vPage.PageSize
        });
    }

    private static async Task<IResult> GetAssetAsync(
        Guid aAssetId,
        ClaimsPrincipal aUser,
        IAssetRepository aAssetRepository,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vAsset = await aAssetRepository.GetAsync(aAssetId, vUserId, aCancellationToken);
        return vAsset is null ? Results.NotFound() : Results.Ok(ToAssetResponse(vAsset));
    }

    // ---- Projects (REQ-FN-026) ------------------------------------------------------------

    private static async Task<IResult> ListProjectsAsync(
        ClaimsPrincipal aUser,
        IProjectRepository aProjectRepository,
        CancellationToken aCancellationToken,
        bool aIncludeArchived = false)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vProjects = await aProjectRepository.ListAsync(vUserId, aIncludeArchived, aCancellationToken);
        return Results.Ok(vProjects.Select(ToProjectResponse).ToList());
    }

    private static async Task<IResult> CreateProjectAsync(
        ProjectCreateRequest aRequest,
        ClaimsPrincipal aUser,
        IProjectRepository aProjectRepository,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(aRequest.Name))
        {
            return Results.BadRequest(new { error = "Project name is required." });
        }

        var vNow = DateTime.UtcNow;
        var vProject = new Project
        {
            ProjectId = Guid.NewGuid(),
            UserId = vUserId,
            Name = aRequest.Name.Trim(),
            Description = aRequest.Description,
            IsArchived = false,
            CreatedUtc = vNow,
            UpdatedUtc = vNow
        };

        await aProjectRepository.InsertAsync(vProject, aCancellationToken);
        return Results.Created($"{ApiRoutes.Projects}/{vProject.ProjectId}", ToProjectResponse(vProject));
    }

    private static async Task<IResult> GetProjectAsync(
        Guid aProjectId,
        ClaimsPrincipal aUser,
        IProjectRepository aProjectRepository,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vProject = await aProjectRepository.GetAsync(aProjectId, vUserId, aCancellationToken);
        return vProject is null ? Results.NotFound() : Results.Ok(ToProjectResponse(vProject));
    }

    private static async Task<IResult> UpdateProjectAsync(
        Guid aProjectId,
        ProjectUpdateRequest aRequest,
        ClaimsPrincipal aUser,
        IProjectRepository aProjectRepository,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        // Owner-filtered read first: a miss means "not found or not owned" — never leak existence.
        var vExisting = await aProjectRepository.GetAsync(aProjectId, vUserId, aCancellationToken);
        if (vExisting is null)
        {
            return Results.NotFound();
        }

        if (string.IsNullOrWhiteSpace(aRequest.Name))
        {
            return Results.BadRequest(new { error = "Project name is required." });
        }

        vExisting.Name = aRequest.Name.Trim();
        vExisting.Description = aRequest.Description;
        vExisting.IsArchived = aRequest.IsArchived;
        vExisting.UpdatedUtc = DateTime.UtcNow;

        await aProjectRepository.UpdateAsync(vExisting, vUserId, aCancellationToken);
        return Results.Ok(ToProjectResponse(vExisting));
    }

    private static async Task<IResult> ArchiveProjectAsync(
        Guid aProjectId,
        ClaimsPrincipal aUser,
        IProjectRepository aProjectRepository,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vExisting = await aProjectRepository.GetAsync(aProjectId, vUserId, aCancellationToken);
        if (vExisting is null)
        {
            return Results.NotFound();
        }

        await aProjectRepository.ArchiveAsync(aProjectId, vUserId, aCancellationToken);
        return Results.Ok(new { archived = true });
    }

    private static async Task<IResult> GetProjectHistoryAsync(
        Guid aProjectId,
        ClaimsPrincipal aUser,
        IProjectRepository aProjectRepository,
        IAssetRepository aAssetRepository,
        CancellationToken aCancellationToken,
        int aPage = 1,
        int aPageSize = 50)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        // Owner-filtered project read first, so history of an unowned project is never revealed.
        var vProject = await aProjectRepository.GetAsync(aProjectId, vUserId, aCancellationToken);
        if (vProject is null)
        {
            return Results.NotFound();
        }

        // Per-project generation history = the project's assets (each carries the full params for
        // one-click reproduce via POST /api/v1/generation-log/{id}/reproduce on the spine).
        var vFilter = new AssetFilterRequest { ProjectId = aProjectId, Page = aPage, PageSize = aPageSize };
        var vHistory = await aAssetRepository.ListAsync(vFilter, vUserId, aCancellationToken);
        return Results.Ok(new PagedResult<AssetResponse>
        {
            Items = vHistory.Items.Select(ToAssetResponse).ToList(),
            TotalCount = vHistory.TotalCount,
            Page = vHistory.Page,
            PageSize = vHistory.PageSize
        });
    }

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

    private static ProjectResponse ToProjectResponse(Project aProject) => new()
    {
        ProjectId = aProject.ProjectId,
        Name = aProject.Name,
        Description = aProject.Description,
        IsArchived = aProject.IsArchived,
        CreatedUtc = aProject.CreatedUtc,
        UpdatedUtc = aProject.UpdatedUtc
    };

    private static string? ResolveUserId(ClaimsPrincipal aUser)
        => aUser.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? aUser.FindFirst("sub")?.Value;
}
