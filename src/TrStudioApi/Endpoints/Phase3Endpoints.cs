using System.Net.Http;
using TrStudio.Engine.DependencyInjection;
using TrStudio.Engine.Domain.ModelSources;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudioApi.Endpoints;

/// <summary>
/// Endpoint mapping for the Phase-3 backend: full model-source CRUD + connection test
/// (<c>/api/v1/admin/sources</c>, REQ-FN-030), the spend ledger / monthly-cap report
/// (<c>/api/v1/admin/spend</c>, REQ-FN-029/NFR-007) and the RunPod serverless admin ops
/// (<c>/api/v1/admin/runpod</c>, REQ-FN-028). Every endpoint is admin-scoped via
/// <see cref="SpineModule.AdminOnlyPolicy"/> (role Owner or Operator).
/// </summary>
/// <remarks>
/// The thin source GET-list lives in <see cref="SetupEndpoints"/> (cluster C); this file adds the
/// remaining source verbs plus the spend and RunPod surfaces without re-mapping that route.
/// </remarks>
public static class Phase3Endpoints
{
    /// <summary>
    /// Maps the Phase-3 sources-CRUD, spend and RunPod endpoints onto the application pipeline.
    /// </summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapPhase3Endpoints(this WebApplication aApp)
    {
        // REQ-FN-030 — model-source CRUD + reachability test (GET-list already mapped by cluster C).
        var vSources = aApp.MapGroup(ApiRoutes.Sources)
            .RequireAuthorization(SpineModule.AdminOnlyPolicy)
            .WithTags("AdminSources");
        vSources.MapGet("{aSourceId:guid}", GetSourceAsync).WithName("GetModelSource");
        vSources.MapPost("", CreateSourceAsync).WithName("CreateModelSource");
        vSources.MapPut("{aSourceId:guid}", UpdateSourceAsync).WithName("UpdateModelSource");
        vSources.MapDelete("{aSourceId:guid}", DeleteSourceAsync).WithName("DeleteModelSource");
        vSources.MapPost("{aSourceId:guid}/test-connection", TestSourceConnectionAsync).WithName("TestModelSourceConnection");

        // REQ-FN-029/NFR-007 — spend-to-date by channel, cap and remaining headroom.
        var vSpend = aApp.MapGroup(ApiRoutes.Spend)
            .RequireAuthorization(SpineModule.AdminOnlyPolicy)
            .WithTags("AdminSpend");
        vSpend.MapGet("", GetSpendAsync).WithName("GetSpendSummary");

        // REQ-FN-028 — RunPod serverless ops (status / start / stop / auto-idle-stop).
        var vRunPod = aApp.MapGroup(ApiRoutes.RunPod)
            .RequireAuthorization(SpineModule.AdminOnlyPolicy)
            .WithTags("AdminRunPod");
        vRunPod.MapGet("", GetRunPodStatusAsync).WithName("GetRunPodStatus");
        vRunPod.MapPost("start", StartRunPodAsync).WithName("StartRunPod");
        vRunPod.MapPost("stop", StopRunPodAsync).WithName("StopRunPod");
        vRunPod.MapPost("auto-idle-stop", SetRunPodAutoIdleStopAsync).WithName("SetRunPodAutoIdleStop");

        return aApp;
    }

    // ----- REQ-FN-030 model sources -----

    /// <summary>REQ-FN-030: returns one model source (tokens are never exposed, only the HasToken flag).</summary>
    private static async Task<IResult> GetSourceAsync(
        Guid aSourceId,
        IModelSourceRepository aRepository,
        CancellationToken aCancellationToken)
    {
        var vSource = await aRepository.GetAsync(aSourceId, aCancellationToken);
        return vSource is null ? Results.NotFound() : Results.Ok(vSource);
    }

    /// <summary>
    /// REQ-FN-030: creates a model source. Any supplied API token is stored encrypted via the
    /// token store and never echoed back.
    /// </summary>
    private static async Task<IResult> CreateSourceAsync(
        ModelSourceUpsertRequest aRequest,
        IModelSourceRepository aRepository,
        IModelSourceTokenStore aTokenStore,
        CancellationToken aCancellationToken)
    {
        if (string.IsNullOrWhiteSpace(aRequest.Name) || string.IsNullOrWhiteSpace(aRequest.BaseUrl))
        {
            return Results.Problem(statusCode: 400, title: "VALIDATION_ERROR", detail: "Name and BaseUrl are required.");
        }

        var vSourceId = aRequest.SourceId is { } vId && vId != Guid.Empty ? vId : Guid.NewGuid();
        var vSource = new ModelSource
        {
            SourceId = vSourceId,
            Name = aRequest.Name,
            Kind = aRequest.Kind,
            BaseUrl = aRequest.BaseUrl,
            IsEnabled = aRequest.IsEnabled,
            Status = "unknown"
        };

        await aRepository.UpsertAsync(vSource, aCancellationToken);
        if (!string.IsNullOrWhiteSpace(aRequest.ApiToken))
        {
            await aTokenStore.SetTokenAsync(vSourceId, aRequest.ApiToken, aCancellationToken);
        }

        var vStored = await aRepository.GetAsync(vSourceId, aCancellationToken);
        return Results.Created($"{ApiRoutes.Sources}/{vSourceId}", vStored);
    }

    /// <summary>
    /// REQ-FN-030: updates a model source. A non-null token replaces (or, when empty, clears) the
    /// stored token; a null token leaves the existing token untouched.
    /// </summary>
    private static async Task<IResult> UpdateSourceAsync(
        Guid aSourceId,
        ModelSourceUpsertRequest aRequest,
        IModelSourceRepository aRepository,
        IModelSourceTokenStore aTokenStore,
        CancellationToken aCancellationToken)
    {
        var vExisting = await aRepository.GetAsync(aSourceId, aCancellationToken);
        if (vExisting is null)
        {
            return Results.NotFound();
        }

        if (string.IsNullOrWhiteSpace(aRequest.Name) || string.IsNullOrWhiteSpace(aRequest.BaseUrl))
        {
            return Results.Problem(statusCode: 400, title: "VALIDATION_ERROR", detail: "Name and BaseUrl are required.");
        }

        var vSource = new ModelSource
        {
            SourceId = aSourceId,
            Name = aRequest.Name,
            Kind = aRequest.Kind,
            BaseUrl = aRequest.BaseUrl,
            IsEnabled = aRequest.IsEnabled,
            Status = vExisting.Status
        };

        await aRepository.UpsertAsync(vSource, aCancellationToken);
        if (aRequest.ApiToken is not null)
        {
            await aTokenStore.SetTokenAsync(aSourceId, aRequest.ApiToken, aCancellationToken);
        }

        var vStored = await aRepository.GetAsync(aSourceId, aCancellationToken);
        return Results.Ok(vStored);
    }

    /// <summary>REQ-FN-030: deletes a model source (and its encrypted token row).</summary>
    private static async Task<IResult> DeleteSourceAsync(
        Guid aSourceId,
        IModelSourceRepository aRepository,
        CancellationToken aCancellationToken)
    {
        var vExisting = await aRepository.GetAsync(aSourceId, aCancellationToken);
        if (vExisting is null)
        {
            return Results.NotFound();
        }

        await aRepository.DeleteAsync(aSourceId, aCancellationToken);
        return Results.NoContent();
    }

    /// <summary>
    /// REQ-FN-030: probes the source base URL (HEAD, falling back to GET) and reports whether it
    /// is reachable, persisting the resulting status label on the source. No token is logged.
    /// </summary>
    private static async Task<IResult> TestSourceConnectionAsync(
        Guid aSourceId,
        IModelSourceRepository aRepository,
        IHttpClientFactory aHttpClientFactory,
        CancellationToken aCancellationToken)
    {
        var vSource = await aRepository.GetAsync(aSourceId, aCancellationToken);
        if (vSource is null)
        {
            return Results.NotFound();
        }

        var vResponse = new SourceTestConnectionResponse { SourceId = aSourceId };
        var vClient = aHttpClientFactory.CreateClient();
        vClient.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            using var vHead = new HttpRequestMessage(HttpMethod.Head, vSource.BaseUrl);
            using var vResult = await SendWithGetFallbackAsync(vClient, vHead, vSource.BaseUrl, aCancellationToken);
            vResponse.Reachable = true;
            vResponse.HttpStatus = (int)vResult.StatusCode;
            vResponse.Status = "ok";
            vResponse.Message = $"Reachable — responded {(int)vResult.StatusCode} {vResult.ReasonPhrase}.";
        }
        catch (Exception vException) when (vException is HttpRequestException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            vResponse.Reachable = false;
            vResponse.Status = "unreachable";
            vResponse.Message = $"Unreachable — {vException.GetType().Name}. Check the base URL and network access.";
        }

        // Persist the probed status label back onto the source (token untouched).
        vSource.Status = vResponse.Status;
        await aRepository.UpsertAsync(vSource, aCancellationToken);
        return Results.Ok(vResponse);
    }

    private static async Task<HttpResponseMessage> SendWithGetFallbackAsync(
        HttpClient aClient,
        HttpRequestMessage aHeadRequest,
        string aUrl,
        CancellationToken aCancellationToken)
    {
        try
        {
            return await aClient.SendAsync(aHeadRequest, HttpCompletionOption.ResponseHeadersRead, aCancellationToken);
        }
        catch (HttpRequestException)
        {
            // Some hosts reject HEAD; fall back to a lightweight GET.
            using var vGet = new HttpRequestMessage(HttpMethod.Get, aUrl);
            return await aClient.SendAsync(vGet, HttpCompletionOption.ResponseHeadersRead, aCancellationToken);
        }
    }

    // ----- REQ-FN-029 spend -----

    /// <summary>REQ-FN-029/NFR-007: returns month-to-date spend by channel, the cap and remaining headroom.</summary>
    private static async Task<IResult> GetSpendAsync(ISpendLedger aSpendLedger, CancellationToken aCancellationToken)
    {
        var vSummary = await aSpendLedger.GetSummaryAsync(aCancellationToken);
        return Results.Ok(vSummary);
    }

    // ----- REQ-FN-028 RunPod ops -----

    /// <summary>REQ-FN-028: reports RunPod status; returns a clean "not configured" state when no key is set.</summary>
    private static async Task<IResult> GetRunPodStatusAsync(IRunPodOps aRunPodOps, CancellationToken aCancellationToken)
        => Results.Ok(await aRunPodOps.GetStatusAsync(aCancellationToken));

    /// <summary>REQ-FN-028: warms the RunPod endpoint (min workers = 1).</summary>
    private static async Task<IResult> StartRunPodAsync(IRunPodOps aRunPodOps, CancellationToken aCancellationToken)
        => Results.Ok(await aRunPodOps.StartAsync(aCancellationToken));

    /// <summary>REQ-FN-028: scales the RunPod endpoint to zero (idle-stop).</summary>
    private static async Task<IResult> StopRunPodAsync(IRunPodOps aRunPodOps, CancellationToken aCancellationToken)
        => Results.Ok(await aRunPodOps.StopAsync(aCancellationToken));

    /// <summary>REQ-FN-028: toggles auto idle-stop (scale-to-zero after an idle job).</summary>
    private static async Task<IResult> SetRunPodAutoIdleStopAsync(
        IRunPodOps aRunPodOps,
        CancellationToken aCancellationToken,
        [Microsoft.AspNetCore.Mvc.FromQuery(Name = "enabled")] bool aEnabled = true)
        => Results.Ok(await aRunPodOps.SetAutoIdleStopAsync(aEnabled, aCancellationToken));
}
