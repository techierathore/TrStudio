using System.Security.Claims;
using Hangfire;
using Microsoft.AspNetCore.SignalR;
using TrStudio.Engine.DependencyInjection;
using TrStudio.Engine.Domain.Jobs;
using TrStudio.Engine.Domain.Progress;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudioApi.Hubs;
using TrStudioApi.Infrastructure;

namespace TrStudioApi.Endpoints;

/// <summary>
/// Endpoint mapping for the spine cluster: generation jobs (<c>/api/v1/jobs</c> — submit,
/// poll, result, list, cancel: the identical sequence for every provider, REQ-FN-002), the
/// generation log with one-click reproduce (<c>/api/v1/generation-log</c>, REQ-FN-018), the
/// admin-guarded Hangfire dashboard (REQ-FN-003) and the SignalR progress bridge (REQ-FN-005).
/// </summary>
/// <remarks>
/// OWNED BY: cluster A (spine). Only that cluster edits this file; other clusters map their
/// endpoints in their own file.
/// </remarks>
public static class SpineEndpoints
{
    /// <summary>
    /// Maps the spine endpoints onto the application pipeline and bridges the engine's
    /// progress broadcaster onto the SignalR progress hub.
    /// </summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapSpineEndpoints(this WebApplication aApp)
    {
        // REQ-FN-005: engine-side IProgressNotifier publications fan out to the caller's
        // per-user SignalR group as "JobProgress" events.
        var vBroadcaster = aApp.Services.GetRequiredService<ProgressBroadcaster>();
        var vHubContext = aApp.Services.GetRequiredService<IHubContext<ProgressHub>>();
        vBroadcaster.Subscribe(aUpdate => vHubContext.Clients
            .Group(ProgressHub.UserGroupName(aUpdate.UserId))
            .SendAsync(ProgressHub.JobProgressEvent, aUpdate));

        var vJobs = aApp.MapGroup(ApiRoutes.Jobs).WithTags("Jobs");

        vJobs.MapPost("", SubmitJobAsync).WithName("SubmitJob");
        vJobs.MapGet("", ListJobsAsync).WithName("ListJobs");
        vJobs.MapGet("{aJobId:guid}", GetJobStatusAsync).WithName("GetJobStatus");
        vJobs.MapGet("{aJobId:guid}/result", GetJobResultAsync).WithName("GetJobResult");
        vJobs.MapPost("{aJobId:guid}/cancel", CancelJobAsync).WithName("CancelJob");

        var vLog = aApp.MapGroup(ApiRoutes.GenerationLog).WithTags("GenerationLog");
        vLog.MapGet("", ListGenerationLogAsync).WithName("ListGenerationLog");
        vLog.MapPost("{aLogId:guid}/reproduce", ReproduceFromLogAsync).WithName("ReproduceFromLog");

        // REQ-FN-003/004: Hangfire dashboard, admin-guarded (AdminOnly policy + dashboard
        // filter), read-only.
        aApp.MapHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new HangfireDashboardAuthorizationFilter() },
            IsReadOnlyFunc = _ => true
        }).RequireAuthorization(SpineModule.AdminOnlyPolicy);

        return aApp;
    }

    private static async Task<IResult> SubmitJobAsync(
        JobSubmitRequest aRequest,
        ClaimsPrincipal aUser,
        IJobOrchestrator aOrchestrator,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var vResponse = await aOrchestrator.SubmitAsync(aRequest, vUserId, aCancellationToken);
            return Results.Ok(vResponse);
        }
        catch (TrStudio.Engine.Domain.Safety.SafetyBlockedException vException)
        {
            // SAFE-01..06 / FN-056..063: a non-disableable safety floor refused the submission. This is
            // a deliberate 403 (never a 500) — the job was persisted Blocked and audited before enqueue.
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "SAFETY_BLOCKED",
                detail: vException.Message,
                extensions: new Dictionary<string, object?> { ["floor"] = vException.Floor.ToString() });
        }
        catch (KeyNotFoundException vException)
        {
            return Results.NotFound(new { error = vException.Message });
        }
        catch (TrStudio.Engine.Domain.Cost.SpendCapExceededException vException)
        {
            // REQ-FN-029/NFR-007: an over-cap submit is blocked with an actionable payment-required
            // problem rather than being silently enqueued.
            return Results.Problem(statusCode: StatusCodes.Status402PaymentRequired, title: "SPEND_CAP_EXCEEDED", detail: vException.Message);
        }
        catch (ArgumentException vException)
        {
            return Results.BadRequest(new { error = vException.Message });
        }
    }

    private static async Task<IResult> ListJobsAsync(
        ClaimsPrincipal aUser,
        IJobOrchestrator aOrchestrator,
        CancellationToken aCancellationToken,
        int aPage = 1,
        int aPageSize = 50)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vPage = await aOrchestrator.ListAsync(vUserId, aPage, aPageSize, aCancellationToken);
        return Results.Ok(vPage);
    }

    private static async Task<IResult> GetJobStatusAsync(
        Guid aJobId,
        ClaimsPrincipal aUser,
        IJobOrchestrator aOrchestrator,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vStatus = await aOrchestrator.GetStatusAsync(aJobId, vUserId, aCancellationToken);
        return vStatus is null ? Results.NotFound() : Results.Ok(vStatus);
    }

    private static async Task<IResult> GetJobResultAsync(
        Guid aJobId,
        ClaimsPrincipal aUser,
        IJobOrchestrator aOrchestrator,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vAssets = await aOrchestrator.GetResultAsync(aJobId, vUserId, aCancellationToken);
        if (vAssets is null)
        {
            return Results.NotFound();
        }

        var vResponses = vAssets.Select(aAsset => new AssetResponse
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
        }).ToList();
        return Results.Ok(vResponses);
    }

    private static async Task<IResult> CancelJobAsync(
        Guid aJobId,
        ClaimsPrincipal aUser,
        IJobOrchestrator aOrchestrator,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vCancelled = await aOrchestrator.CancelAsync(aJobId, vUserId, aCancellationToken);
        return vCancelled switch
        {
            null => Results.NotFound(),
            false => Results.Conflict(new { error = "The job has already completed." }),
            true => Results.Ok(new { cancelled = true })
        };
    }

    private static async Task<IResult> ListGenerationLogAsync(
        ClaimsPrincipal aUser,
        TrStudio.Models.Repositories.IGenerationLogRepository aLogRepository,
        CancellationToken aCancellationToken,
        int aPage = 1,
        int aPageSize = 50)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vPage = await aLogRepository.ListAsync(vUserId, aPage, aPageSize, aCancellationToken);
        return Results.Ok(vPage);
    }

    private static async Task<IResult> ReproduceFromLogAsync(
        Guid aLogId,
        ClaimsPrincipal aUser,
        IJobOrchestrator aOrchestrator,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var vResponse = await aOrchestrator.ReproduceAsync(aLogId, vUserId, aCancellationToken);
            return vResponse is null ? Results.NotFound() : Results.Ok(vResponse);
        }
        catch (KeyNotFoundException vException)
        {
            return Results.Conflict(new { error = vException.Message });
        }
    }

    private static string? ResolveUserId(ClaimsPrincipal aUser)
        => aUser.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? aUser.FindFirst("sub")?.Value;
}
