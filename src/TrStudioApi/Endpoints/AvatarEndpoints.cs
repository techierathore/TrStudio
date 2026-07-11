using System.Security.Claims;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Services;

namespace TrStudioApi.Endpoints;

/// <summary>
/// Endpoint mapping for the flagship avatar pipeline (FN-037/038/039/040/BRD-28..31): the
/// talking-avatar submit (<c>/api/v1/avatar/talking</c> — tier-routed lip-sync, FN-037/039), the
/// end-to-end script→video pipeline (<c>/api/v1/avatar/script-to-video</c>, FN-038) and
/// caption/subtitle generation (<c>/api/v1/avatar/captions</c>, FN-040). Every user-scoped handler
/// resolves the caller's AppManager user id and passes it into the owner-filtered pipeline (BRD-81);
/// avatar jobs run on the job spine so the non-disableable safety floors apply (FN-063).
/// </summary>
public static class AvatarEndpoints
{
    /// <summary>
    /// Maps the avatar endpoints onto the application pipeline.
    /// </summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapAvatarEndpoints(this WebApplication aApp)
    {
        var vAvatar = aApp.MapGroup(ApiRoutes.Avatar).WithTags("Avatar");
        vAvatar.MapPost("talking", SubmitTalkingAvatarAsync).WithName("SubmitTalkingAvatar");
        vAvatar.MapPost("script-to-video", RunScriptToVideoAsync).WithName("RunScriptToVideo");
        vAvatar.MapPost("captions", GenerateCaptionsAsync).WithName("GenerateAvatarCaptions");
        return aApp;
    }

    /// <summary>FN-037/039: submits a tier-routed talking-avatar lip-sync job; degrades cleanly without a model/key.</summary>
    private static async Task<IResult> SubmitTalkingAvatarAsync(
        AvatarTalkingRequest aRequest,
        ClaimsPrincipal aUser,
        IAvatarPipeline aPipeline,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vResponse = await aPipeline.SubmitTalkingAvatarAsync(aRequest, vUserId, aCancellationToken);
        if (vResponse is null)
        {
            return Results.NotFound(new { error = "Character not found or not owned by the caller." });
        }

        // A safety-floor block is a deliberate 403 (FN-063), never a server error.
        return vResponse.Status == "blocked"
            ? Results.Json(vResponse, statusCode: StatusCodes.Status403Forbidden)
            : Results.Ok(vResponse);
    }

    /// <summary>FN-038: runs the end-to-end script→TTS→lip-sync→captions pipeline; stages degrade independently.</summary>
    private static async Task<IResult> RunScriptToVideoAsync(
        ScriptToVideoRequest aRequest,
        ClaimsPrincipal aUser,
        IAvatarPipeline aPipeline,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(aRequest.Script))
        {
            return Results.BadRequest(new { error = "Script is required." });
        }

        var vResponse = await aPipeline.RunScriptToVideoAsync(aRequest, vUserId, aCancellationToken);
        return vResponse is null
            ? Results.NotFound(new { error = "Character not found or not owned by the caller." })
            : Results.Ok(vResponse);
    }

    /// <summary>FN-040: generates a valid, script-timed SRT/VTT caption sidecar.</summary>
    private static IResult GenerateCaptionsAsync(
        SubtitleRequest aRequest,
        ClaimsPrincipal aUser,
        IAvatarPipeline aPipeline)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(aRequest.Script))
        {
            return Results.BadRequest(new { error = "Script is required." });
        }

        return Results.Ok(aPipeline.GenerateSubtitles(aRequest));
    }

    private static string? ResolveUserId(ClaimsPrincipal aUser)
        => aUser.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? aUser.FindFirst("sub")?.Value;
}
