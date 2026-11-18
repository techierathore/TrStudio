using System.Security.Claims;
using TrStudio.Engine.DependencyInjection;
using TrStudio.Engine.Domain.Twin;
using TrStudio.Engine.Domain.Venice;
using TrStudio.InProcess.Hosting;
using TrStudio.Models;
using TrStudio.Models.Contracts;

namespace TrStudio.InProcess.Routes;

/// <summary>
/// Endpoints for digital twins (REQ-FN-041, REQ-FN-105 to REQ-FN-109): upload a twin's files, create a
/// twin, list the user's twins, make a clip of a line, and the owner's on/off setting (admin only).
/// A refusal for twins being off is a 409 and a refusal for missing consent a 403, both with the plain
/// reason, and nothing is sent to Venice.
/// </summary>
public static class TwinEndpoints
{
    /// <summary>Maps the digital twin endpoints onto the route table.</summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapTwinEndpoints(this WebApplication aApp)
    {
        var vAvatar = aApp.MapGroup(ApiRoutes.Avatar).WithTags("DigitalTwin");
        vAvatar.MapPost("twin-uploads", UploadAsync).WithName("UploadTwinFile");
        // REQ-FN-051/052: a frame pinned on Video studio is uploaded as the creator's own picture.
        aApp.MapPost(ApiRoutes.FrameUploads, UploadFrameAsync).WithTags("Video").WithName("UploadVideoFrame");
        vAvatar.MapGet("digital-twins/status", GetPolicyAsync).WithName("GetDigitalTwinStatus");
        vAvatar.MapPost("digital-twin", CreateAsync).WithName("CreateDigitalTwin");
        vAvatar.MapGet("digital-twins", ListAsync).WithName("ListDigitalTwins");
        vAvatar.MapPost("digital-twins/{aTwinId:guid}/clips", MakeClipAsync).WithName("MakeDigitalTwinClip");

        var vPolicy = aApp.MapGroup(ApiRoutes.AdminPolicy)
            .RequireAuthorization(SpineModule.AdminOnlyPolicy)
            .WithTags("AdminSafety");
        vPolicy.MapGet("twins", GetPolicyAsync).WithName("GetDigitalTwinPolicy");
        vPolicy.MapPut("twins", SetPolicyAsync).WithName("SetDigitalTwinPolicy");
        return aApp;
    }

    private static async Task<IResult> UploadAsync(DigitalTwinUploadRequest aRequest, ClaimsPrincipal aUser, IDigitalTwinService aService, CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var vBytes = Convert.FromBase64String(aRequest.Base64Content ?? string.Empty);
            var vAssetId = await aService.UploadAsync(aRequest.Kind, aRequest.FileName, vBytes, vUserId, aCancellationToken);
            return Results.Ok(new DigitalTwinUploadResponse { AssetId = vAssetId });
        }
        catch (FormatException)
        {
            return Results.BadRequest(new { error = "Base64Content is not valid base64." });
        }
        catch (ArgumentException vException)
        {
            return Results.BadRequest(new { error = vException.Message });
        }
    }

    private static async Task<IResult> UploadFrameAsync(DigitalTwinUploadRequest aRequest, ClaimsPrincipal aUser, IDigitalTwinService aService, CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var vBytes = Convert.FromBase64String(aRequest.Base64Content ?? string.Empty);
            var vAssetId = await aService.UploadForAsync("frame", "picture", aRequest.FileName, vBytes, vUserId, aCancellationToken);
            return Results.Ok(new DigitalTwinUploadResponse { AssetId = vAssetId });
        }
        catch (FormatException)
        {
            return Results.BadRequest(new { error = "Base64Content is not valid base64." });
        }
        catch (ArgumentException vException)
        {
            return Results.BadRequest(new { error = vException.Message });
        }
    }

    private static Task<IResult> CreateAsync(DigitalTwinCreateRequest aRequest, ClaimsPrincipal aUser, IDigitalTwinService aService, CancellationToken aCancellationToken)
        => RunAsync(aUser, async aUserId => Results.Ok(await aService.CreateAsync(aRequest, aUserId, aCancellationToken)));

    private static Task<IResult> ListAsync(ClaimsPrincipal aUser, IDigitalTwinService aService, CancellationToken aCancellationToken)
        => RunAsync(aUser, async aUserId => Results.Ok(await aService.ListAsync(aUserId, aCancellationToken)));

    private static Task<IResult> MakeClipAsync(Guid aTwinId, DigitalTwinClipRequest aRequest, ClaimsPrincipal aUser, IDigitalTwinService aService, CancellationToken aCancellationToken)
        => RunAsync(aUser, async aUserId =>
        {
            var vJobId = await aService.QueueClipAsync(aTwinId, aUserId, aRequest.Line, null, aRequest.Duration, aRequest.Resolution, aCancellationToken);
            return Results.Ok(new DigitalTwinClipResponse { JobId = vJobId });
        });

    private static async Task<IResult> GetPolicyAsync(IDigitalTwinService aService, CancellationToken aCancellationToken)
        => Results.Ok(new DigitalTwinPolicyResponse { Enabled = await aService.GetEnabledAsync(aCancellationToken) });

    private static async Task<IResult> SetPolicyAsync(DigitalTwinPolicyUpdateRequest aRequest, IDigitalTwinService aService, CancellationToken aCancellationToken)
    {
        await aService.SetEnabledAsync(aRequest.Enabled, aCancellationToken);
        return Results.Ok(new DigitalTwinPolicyResponse { Enabled = aRequest.Enabled });
    }

    /// <summary>Runs a handler as the signed-in user and maps the twin refusals and provider failures to plain answers.</summary>
    private static async Task<IResult> RunAsync(ClaimsPrincipal aUser, Func<string, Task<IResult>> aHandler)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            return await aHandler(vUserId);
        }
        catch (DigitalTwinRefusedException vRefusal)
        {
            var vStatus = vRefusal.Reason switch
            {
                DigitalTwinRefusal.TurnedOff => StatusCodes.Status409Conflict,
                DigitalTwinRefusal.ConsentMissing => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status404NotFound
            };
            return Results.Json(new { error = vRefusal.Message, reason = vRefusal.Reason.ToString() }, statusCode: vStatus);
        }
        catch (ArgumentException vException)
        {
            return Results.BadRequest(new { error = vException.Message });
        }
        catch (TrStudio.Engine.Domain.Safety.SafetyBlockedException vException)
        {
            return Results.Json(new { error = vException.Message, reason = "SafetyBlocked" }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (TrStudio.Engine.Domain.Cost.SpendCapExceededException vException)
        {
            return Results.Json(new { error = vException.Message, reason = "SpendCapExceeded" }, statusCode: StatusCodes.Status402PaymentRequired);
        }
        catch (VeniceApiException vException)
        {
            return Results.Json(new { error = vException.Message, reason = "ProviderFailed" }, statusCode: StatusCodes.Status502BadGateway);
        }
        catch (InvalidOperationException vException)
        {
            return Results.Json(new { error = vException.Message, reason = "NotReady" }, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static string? ResolveUserId(ClaimsPrincipal aUser)
        => aUser.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? aUser.FindFirst("sub")?.Value;
}
