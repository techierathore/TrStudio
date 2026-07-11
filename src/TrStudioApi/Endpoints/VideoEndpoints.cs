using System.Security.Claims;
using TrStudio.Engine.Domain.Video;
using TrStudio.Models;
using TrStudio.Models.Contracts;

namespace TrStudioApi.Endpoints;

/// <summary>
/// Endpoint mapping for the Phase-7 video vertical (FN-051..054/BRD-42..45): I2V/T2V generation
/// (<c>/api/v1/video/generate</c> — tier-routed, FN-051/052), the frame-interpolation and upscale
/// post-steps (<c>/api/v1/video/interpolate</c> + <c>/upscale</c>, FN-053) and the trim/join/reframe/
/// add-audio + MP4 export editor (<c>/api/v1/video/edit</c>, FN-054). Every user-scoped handler
/// resolves the caller's AppManager user id and passes it into the owner-filtered services (BRD-81);
/// generation jobs run on the job spine so the non-disableable safety floors apply (FN-063).
/// </summary>
public static class VideoEndpoints
{
    /// <summary>
    /// Maps the video endpoints onto the application pipeline.
    /// </summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapVideoEndpoints(this WebApplication aApp)
    {
        var vVideo = aApp.MapGroup(ApiRoutes.Video).WithTags("Video");
        vVideo.MapPost("generate", GenerateAsync).WithName("GenerateVideo");
        vVideo.MapPost("interpolate", InterpolateAsync).WithName("InterpolateVideo");
        vVideo.MapPost("upscale", UpscaleAsync).WithName("UpscaleVideo");
        vVideo.MapPost("edit", EditAsync).WithName("EditVideo");
        return aApp;
    }

    /// <summary>FN-051/052: submits a tier-routed I2V/T2V job; degrades cleanly without a model/key.</summary>
    private static async Task<IResult> GenerateAsync(
        VideoGenerateRequest aRequest,
        ClaimsPrincipal aUser,
        IVideoGenerationService aService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vResponse = await aService.SubmitAsync(aRequest, vUserId, aCancellationToken);

        // A safety-floor block is a deliberate 403 (FN-063); a validation/cost failure is a 400.
        return vResponse.Status switch
        {
            "blocked" => Results.Json(vResponse, statusCode: StatusCodes.Status403Forbidden),
            "error" => Results.BadRequest(vResponse),
            _ => Results.Ok(vResponse)
        };
    }

    /// <summary>FN-053: frame-interpolation post-step; degrades cleanly without a RIFE/FILM model.</summary>
    private static async Task<IResult> InterpolateAsync(
        VideoPostStepRequest aRequest,
        ClaimsPrincipal aUser,
        IVideoPostProcessor aProcessor,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vResponse = await aProcessor.InterpolateAsync(aRequest, vUserId, aCancellationToken);
        return vResponse is null
            ? Results.NotFound(new { error = "Source video asset not found or not owned by the caller." })
            : Results.Ok(vResponse);
    }

    /// <summary>FN-053: video-upscale post-step; degrades cleanly without an upscale model.</summary>
    private static async Task<IResult> UpscaleAsync(
        VideoPostStepRequest aRequest,
        ClaimsPrincipal aUser,
        IVideoPostProcessor aProcessor,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vResponse = await aProcessor.UpscaleAsync(aRequest, vUserId, aCancellationToken);
        return vResponse is null
            ? Results.NotFound(new { error = "Source video asset not found or not owned by the caller." })
            : Results.Ok(vResponse);
    }

    /// <summary>FN-054: trim/join/reframe/add-audio + MP4 export; real via ffmpeg or a clean degrade.</summary>
    private static async Task<IResult> EditAsync(
        VideoEditRequest aRequest,
        ClaimsPrincipal aUser,
        IVideoEditor aEditor,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vResponse = await aEditor.EditAsync(aRequest, vUserId, aCancellationToken);
        if (vResponse is null)
        {
            return Results.NotFound(new { error = "Primary source video asset not found or not owned by the caller." });
        }

        return vResponse.Status == "error"
            ? Results.BadRequest(vResponse)
            : Results.Ok(vResponse);
    }

    private static string? ResolveUserId(ClaimsPrincipal aUser)
        => aUser.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? aUser.FindFirst("sub")?.Value;
}
