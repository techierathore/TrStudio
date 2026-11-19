using TrStudio.Engine.DependencyInjection;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.InProcess.Hosting;
using TrStudio.Models;
using TrStudio.Models.Contracts;

namespace TrStudio.InProcess.Routes;

/// <summary>
/// REQ-NFR-010 (ADR-030): the owner's "Keep log rows for N days" setting on Content policy
/// (<c>/api/v1/admin/policy/retention</c>). Saving removes the generation-log and audit rows older than N days
/// at once; pictures, clips and provenance are never removed.
/// </summary>
public static class RetentionEndpoints
{
    /// <summary>Maps the retention endpoints onto the route table.</summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapRetentionEndpoints(this WebApplication aApp)
    {
        var vPolicy = aApp.MapGroup(ApiRoutes.AdminPolicy)
            .RequireAuthorization(SpineModule.AdminOnlyPolicy)
            .WithTags("AdminSafety");
        vPolicy.MapGet("retention", GetAsync).WithName("GetLogRetention");
        vPolicy.MapPut("retention", SetAsync).WithName("SetLogRetention");
        return aApp;
    }

    private static async Task<IResult> GetAsync(LogRetentionService aService, CancellationToken aCancellationToken)
        => Results.Ok(new LogRetentionResult { Days = await aService.GetDaysAsync(aCancellationToken) });

    private static async Task<IResult> SetAsync(LogRetentionRequest aRequest, LogRetentionService aService, CancellationToken aCancellationToken)
    {
        try
        {
            return Results.Ok(await aService.SaveAsync(aRequest.Days, aCancellationToken));
        }
        catch (ArgumentOutOfRangeException vException)
        {
            return Results.Problem(statusCode: 400, title: "VALIDATION_ERROR", detail: vException.Message.Split('\n')[0]);
        }
    }
}
