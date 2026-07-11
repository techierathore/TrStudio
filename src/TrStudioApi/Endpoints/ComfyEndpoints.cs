using System.Security.Claims;
using TrStudio.Engine.DependencyInjection;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudioApi.Endpoints;

/// <summary>
/// Endpoint mapping for the Phase-6 ComfyUI pipeline (<c>/api/v1/admin/comfy/*</c>,
/// FN-043..050/BRD-34..41), every endpoint admin-scoped via <see cref="SpineModule.AdminOnlyPolicy"/>
/// (role Owner or Operator): import + parse (FN-043), map inputs → params (FN-044), dependency scan
/// (FN-047), run-debug bundle (FN-046), save-as-model (FN-048), workflow versioning + pin (FN-049)
/// and the sandbox-smoke go-live gate (FN-050). The workflow graph is stored server-side and never
/// exposed through the creator-facing registry read.
/// </summary>
/// <remarks>
/// OWNED BY: cluster Phase-6 (ComfyUI pipeline). The admin UI drives these endpoints; the
/// <see cref="ComfyModule"/> registers the backing services.
/// </remarks>
public static class ComfyEndpoints
{
    /// <summary>
    /// Maps the ComfyUI pipeline endpoints onto the application pipeline.
    /// </summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapComfyEndpoints(this WebApplication aApp)
    {
        var vComfy = aApp.MapGroup(ApiRoutes.Comfy)
            .RequireAuthorization(SpineModule.AdminOnlyPolicy)
            .WithTags("AdminComfy");

        // FN-043 — import + parse.
        vComfy.MapPost("import", ImportAsync).WithName("ComfyImport");
        // FN-044 — map inputs → registry params.
        vComfy.MapPost("map", MapParamsAsync).WithName("ComfyMap");
        // FN-047 — dependency scan (custom nodes / model files).
        vComfy.MapPost("dependencies", ScanDependenciesAsync).WithName("ComfyDependencies");
        // FN-046 — run-debug bundle (exact submitted JSON + missing deps).
        vComfy.MapPost("debug", BuildDebugAsync).WithName("ComfyDebug");
        // FN-048 — save imported + mapped workflow as a registered model (graph hidden).
        vComfy.MapPost("save-as-model", SaveAsModelAsync).WithName("ComfySaveAsModel");

        // FN-049 — workflows + versioning.
        vComfy.MapGet("workflows", ListWorkflowsAsync).WithName("ComfyListWorkflows");
        vComfy.MapGet("workflows/{aWorkflowId:guid}/versions", ListVersionsAsync).WithName("ComfyListVersions");
        vComfy.MapPost("workflows/{aWorkflowId:guid}/versions", AddVersionAsync).WithName("ComfyAddVersion");

        // FN-049 — pin / served-version round-trip.
        vComfy.MapPost("models/{aModelId}/pin", PinVersionAsync).WithName("ComfyPinVersion");
        vComfy.MapGet("models/{aModelId}/served-version", GetServedVersionAsync).WithName("ComfyServedVersion");

        // FN-050 — sandbox-smoke gate.
        vComfy.MapPost("models/{aModelId}/smoke", RunSmokeAsync).WithName("ComfyRunSmoke");
        vComfy.MapPost("models/{aModelId}/go-live", GoLiveAsync).WithName("ComfyGoLive");

        return aApp;
    }

    /// <summary>FN-043: parses the workflow; malformed JSON is a clean 400 parse-error, never a 500.</summary>
    private static IResult ImportAsync(ComfyImportRequest aRequest, IComfyPipelineService aService)
    {
        var vResult = aService.Parse(aRequest.WorkflowJson);
        return vResult.Ok
            ? Results.Ok(vResult)
            : Results.Problem(statusCode: 400, title: "COMFY_PARSE_ERROR", detail: vResult.Error);
    }

    /// <summary>FN-044: maps the chosen inputs to registry params (internal/link inputs hidden).</summary>
    private static IResult MapParamsAsync(ComfyMapRequest aRequest, IComfyPipelineService aService)
    {
        var vParsed = aService.Parse(aRequest.WorkflowJson);
        if (!vParsed.Ok)
        {
            return Results.Problem(statusCode: 400, title: "COMFY_PARSE_ERROR", detail: vParsed.Error);
        }

        return Results.Ok(aService.MapParams(aRequest.WorkflowJson, aRequest.Mappings));
    }

    /// <summary>FN-047: reports required + missing custom nodes / model files.</summary>
    private static IResult ScanDependenciesAsync(ComfyDependencyRequest aRequest, IComfyPipelineService aService)
    {
        var vParsed = aService.Parse(aRequest.WorkflowJson);
        if (!vParsed.Ok)
        {
            return Results.Problem(statusCode: 400, title: "COMFY_PARSE_ERROR", detail: vParsed.Error);
        }

        return Results.Ok(aService.ScanDependencies(aRequest.WorkflowJson));
    }

    /// <summary>FN-046: builds the exact submitted JSON + per-node status + missing-dependency detection.</summary>
    private static IResult BuildDebugAsync(ComfyDebugRequest aRequest, IComfyPipelineService aService)
    {
        var vParsed = aService.Parse(aRequest.WorkflowJson);
        if (!vParsed.Ok)
        {
            return Results.Problem(statusCode: 400, title: "COMFY_PARSE_ERROR", detail: vParsed.Error);
        }

        return Results.Ok(aService.BuildDebug(aRequest.WorkflowJson, aRequest.ParamsJson));
    }

    /// <summary>FN-048: turns the imported + mapped workflow into a registered model (graph hidden from creators).</summary>
    private static async Task<IResult> SaveAsModelAsync(
        ComfySaveAsModelRequest aRequest,
        ClaimsPrincipal aUser,
        IComfyPipelineService aService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var vModel = await aService.SaveAsModelAsync(aRequest, vUserId, aCancellationToken);
            return Results.Ok(vModel);
        }
        catch (ArgumentException vException)
        {
            return Results.Problem(statusCode: 400, title: "COMFY_SAVE_ERROR", detail: vException.Message);
        }
    }

    /// <summary>FN-049: lists the caller's imported workflows.</summary>
    private static async Task<IResult> ListWorkflowsAsync(
        ClaimsPrincipal aUser,
        IComfyWorkflowRepository aRepository,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await aRepository.ListWorkflowsAsync(vUserId, aCancellationToken));
    }

    /// <summary>FN-049: lists all versions of a workflow (owner-filtered).</summary>
    private static async Task<IResult> ListVersionsAsync(
        Guid aWorkflowId,
        ClaimsPrincipal aUser,
        IComfyPipelineService aService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vVersions = await aService.ListVersionsAsync(aWorkflowId, vUserId, aCancellationToken);
        return vVersions is null ? Results.NotFound() : Results.Ok(vVersions);
    }

    /// <summary>FN-049: adds a new workflow version without changing what any live model serves.</summary>
    private static async Task<IResult> AddVersionAsync(
        Guid aWorkflowId,
        ComfyAddVersionRequest aRequest,
        ClaimsPrincipal aUser,
        IComfyPipelineService aService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var vVersion = await aService.AddVersionAsync(aWorkflowId, aRequest, vUserId, aCancellationToken);
            return Results.Ok(vVersion);
        }
        catch (ArgumentException vException)
        {
            return Results.Problem(statusCode: 400, title: "COMFY_PARSE_ERROR", detail: vException.Message);
        }
        catch (InvalidOperationException vException)
        {
            return Results.Problem(statusCode: 404, title: "COMFY_WORKFLOW_NOT_FOUND", detail: vException.Message);
        }
    }

    /// <summary>FN-049: pins the model to a known-good workflow version.</summary>
    private static async Task<IResult> PinVersionAsync(
        string aModelId,
        ComfyPinRequest aRequest,
        IComfyPipelineService aService,
        CancellationToken aCancellationToken)
    {
        try
        {
            await aService.PinVersionAsync(aModelId, aRequest.VersionId, aCancellationToken);
            return Results.Ok(new { modelId = aModelId, versionId = aRequest.VersionId });
        }
        catch (InvalidOperationException vException)
        {
            return Results.Problem(statusCode: 404, title: "COMFY_PIN_NOT_FOUND", detail: vException.Message);
        }
    }

    /// <summary>FN-049: returns the version the model currently serves (its pin).</summary>
    private static async Task<IResult> GetServedVersionAsync(
        string aModelId,
        IComfyPipelineService aService,
        CancellationToken aCancellationToken)
    {
        var vVersionId = await aService.GetServedVersionAsync(aModelId, aCancellationToken);
        return vVersionId is null
            ? Results.NotFound()
            : Results.Ok(new { modelId = aModelId, servedVersionId = vVersionId });
    }

    /// <summary>FN-050: runs the sandbox smoke against the pinned version and records the verdict.</summary>
    private static async Task<IResult> RunSmokeAsync(
        string aModelId,
        IComfyPipelineService aService,
        CancellationToken aCancellationToken)
    {
        try
        {
            return Results.Ok(await aService.RunSmokeAsync(aModelId, aCancellationToken));
        }
        catch (InvalidOperationException vException)
        {
            return Results.Problem(statusCode: 404, title: "COMFY_MODEL_NOT_FOUND", detail: vException.Message);
        }
    }

    /// <summary>FN-050: sets the model live; refused with 409 unless the pinned version has a passing smoke.</summary>
    private static async Task<IResult> GoLiveAsync(
        string aModelId,
        IComfyPipelineService aService,
        CancellationToken aCancellationToken)
    {
        try
        {
            return Results.Ok(await aService.GoLiveAsync(aModelId, aCancellationToken));
        }
        catch (InvalidOperationException vException)
        {
            // A missing model/version is a 404; a blocked go-live (no passing smoke) is a 409.
            return vException.Message.Contains("was not found", StringComparison.OrdinalIgnoreCase)
                || vException.Message.Contains("not pinned", StringComparison.OrdinalIgnoreCase)
                ? Results.Problem(statusCode: 404, title: "COMFY_MODEL_NOT_FOUND", detail: vException.Message)
                : Results.Problem(statusCode: 409, title: "COMFY_SMOKE_GATE_BLOCKED", detail: vException.Message);
        }
    }

    private static string? ResolveUserId(ClaimsPrincipal aUser)
        => aUser.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? aUser.FindFirst("sub")?.Value;
}
