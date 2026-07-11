using System.Security.Claims;
using TrStudio.Engine.DependencyInjection;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudioApi.Endpoints;

/// <summary>
/// Endpoint mapping for the safety floors surface (FN-058/061/062, SAFE-03/05/06). Adds the
/// abuse-reporting endpoint (<c>/api/v1/abuse-reports</c>), the shared face/voice consent recorder
/// (<c>/api/v1/consent</c>), the admin-scoped full generation audit trail
/// (<c>/api/v1/admin/audit</c>) and the admin-scoped NSFW-mode policy surface
/// (<c>/api/v1/admin/policy/nsfw</c>). The non-disableable floors (SAFE-01..06) are enforced in the
/// generation pipeline itself, not here — no endpoint can toggle them.
/// </summary>
public static class SafetyEndpoints
{
    /// <summary>
    /// Maps the safety endpoints onto the application pipeline.
    /// </summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapSafetyEndpoints(this WebApplication aApp)
    {
        // Abuse reporting — any authenticated caller may file a report (SAFE-06 / BRD-65).
        aApp.MapPost(ApiRoutes.AbuseReports, FileAbuseReportAsync).WithName("FileAbuseReport").WithTags("Safety");

        // Recorded consent for real-person face/voice work (SAFE-03 / BRD-62).
        aApp.MapPost(ApiRoutes.Consent, RecordConsentAsync).WithName("RecordConsent").WithTags("Safety");

        // Full generation audit trail — admin-scoped (SAFE-06 / BRD-65).
        aApp.MapGet(ApiRoutes.AdminAudit, ListAuditAsync)
            .RequireAuthorization(SpineModule.AdminOnlyPolicy)
            .WithName("ListSafetyAudit")
            .WithTags("AdminSafety");

        // NSFW-mode policy — admin-scoped (F-NSFW / BRD-79). Off by default; auto-off in multi-user.
        var vPolicy = aApp.MapGroup(ApiRoutes.AdminPolicy)
            .RequireAuthorization(SpineModule.AdminOnlyPolicy)
            .WithTags("AdminSafety");
        vPolicy.MapGet("nsfw", GetNsfwPolicyAsync).WithName("GetNsfwPolicy");
        vPolicy.MapPut("nsfw", SetNsfwPolicyAsync).WithName("SetNsfwPolicy");

        return aApp;
    }

    /// <summary>FN-061 / SAFE-06: files and persists an abuse report.</summary>
    private static async Task<IResult> FileAbuseReportAsync(
        AbuseReportRequest aRequest,
        ClaimsPrincipal aUser,
        IAbuseReportService aService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vResponse = await aService.FileAsync(aRequest, vUserId, aCancellationToken);
        return Results.Ok(vResponse);
    }

    /// <summary>FN-058 / SAFE-03: records recorded-consent evidence for a real-person subject (face or voice).</summary>
    private static async Task<IResult> RecordConsentAsync(
        ConsentRecordRequest aRequest,
        ClaimsPrincipal aUser,
        IConsentService aConsentService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vResponse = await aConsentService.RecordAsync(aRequest, vUserId, aCancellationToken);
        return vResponse.Recorded ? Results.Ok(vResponse) : Results.BadRequest(vResponse);
    }

    /// <summary>FN-061 / SAFE-06: lists the deployment-wide generation audit trail (allowed AND blocked).</summary>
    private static async Task<IResult> ListAuditAsync(
        ISafetyAuditRepository aRepository,
        CancellationToken aCancellationToken,
        int aPage = 1,
        int aPageSize = 50)
    {
        var vPage = await aRepository.ListAsync(aPage, aPageSize, aCancellationToken);
        return Results.Ok(vPage);
    }

    /// <summary>FN-062 / F-NSFW: reports the effective NSFW-mode state (stored, personal-mode, effective).</summary>
    private static async Task<IResult> GetNsfwPolicyAsync(ISafetyPolicyService aPolicyService, CancellationToken aCancellationToken)
        => Results.Ok(await aPolicyService.GetNsfwPolicyAsync(aCancellationToken));

    /// <summary>FN-062 / F-NSFW: sets the owner NSFW-mode preference; enabling is refused in multi-user/SaaS.</summary>
    private static async Task<IResult> SetNsfwPolicyAsync(
        NsfwPolicyUpdateRequest aRequest,
        ISafetyPolicyService aPolicyService,
        CancellationToken aCancellationToken)
    {
        var vResponse = await aPolicyService.SetNsfwEnabledAsync(aRequest.Enabled, aCancellationToken);

        // A refused enable in multi-user mode is a deliberate 409, not a server error — the floors and
        // the auto-off rule are the policy working.
        if (aRequest.Enabled && !vResponse.EffectiveNsfwEnabled && !vResponse.PersonalMode)
        {
            return Results.Json(vResponse, statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Ok(vResponse);
    }

    private static string? ResolveUserId(ClaimsPrincipal aUser)
        => aUser.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? aUser.FindFirst("sub")?.Value;
}
