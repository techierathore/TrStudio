using System.Security.Claims;
using TrStudio.Engine.Domain.Sequence;
using TrStudio.InProcess.Hosting;
using TrStudio.Models;

namespace TrStudio.InProcess.Routes;

/// <summary>
/// Multi-shot assembly routes (REQ-FN-042) under <c>/api/v1/avatar/sequences</c>: Venice's price of
/// every shot, start a sequence, read one, list mine. Every handler passes the caller's user id into the
/// owner-scoped service (BRD-81).
/// </summary>
public static class SequenceEndpoints
{
    /// <summary>Maps the sequence routes.</summary>
    /// <param name="aApp">The route table.</param>
    /// <returns>The same table for chaining.</returns>
    public static WebApplication MapSequenceEndpoints(this WebApplication aApp)
    {
        var vSequences = aApp.MapGroup(ApiRoutes.Avatar + "/sequences").WithTags("Sequences");
        vSequences.MapPost("quote", QuoteAsync).WithName("QuoteSequence");
        vSequences.MapPost("", StartAsync).WithName("StartSequence");
        vSequences.MapGet("{aSequenceId:guid}", GetAsync).WithName("GetSequence");
        vSequences.MapGet("", ListAsync).WithName("ListSequences");
        return aApp;
    }

    private static async Task<IResult> QuoteAsync(SequenceRequest aRequest, ClaimsPrincipal aUser, ClipSequenceService aService, CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var vQuote = await aService.QuoteAsync(aRequest ?? new SequenceRequest(), vUserId, aCancellationToken);
            return Results.Ok(new { totalUsd = vQuote.TotalUsd, perShot = vQuote.PerShot });
        }
        catch (ArgumentException vException)
        {
            return Results.BadRequest(new { error = vException.Message });
        }
        catch (KeyNotFoundException vException)
        {
            return Results.NotFound(new { error = vException.Message });
        }
        catch (Exception vException) when (vException is not OperationCanceledException)
        {
            return Results.Json(new { error = vException.Message }, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> StartAsync(SequenceRequest aRequest, ClaimsPrincipal aUser, ClipSequenceService aService, CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var vId = await aService.StartAsync(aRequest ?? new SequenceRequest(), vUserId, aCancellationToken);
            return Results.Ok(new { sequenceId = vId });
        }
        catch (ArgumentException vException)
        {
            return Results.BadRequest(new { error = vException.Message });
        }
        catch (KeyNotFoundException vException)
        {
            return Results.NotFound(new { error = vException.Message });
        }
    }

    private static async Task<IResult> GetAsync(Guid aSequenceId, ClaimsPrincipal aUser, ClipSequenceService aService, CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vSequence = await aService.GetAsync(aSequenceId, vUserId, aCancellationToken);
        return vSequence is null ? Results.NotFound(new { error = "That sequence was not found." }) : Results.Ok(ToView(vSequence));
    }

    private static async Task<IResult> ListAsync(ClaimsPrincipal aUser, ClipSequenceService aService, CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok((await aService.ListAsync(vUserId, aCancellationToken)).Select(ToView).ToList());
    }

    private static Dictionary<string, object?> ToView(ClipSequence aSequence)
    {
        var vSubjectKey = aSequence.SubjectKind == SequenceSubject.Twin ? "twinId" : "characterId";
        var vView = new Dictionary<string, object?>
        {
            ["sequenceId"] = aSequence.SequenceId,
            ["state"] = aSequence.State,
            ["message"] = aSequence.Message,
            [vSubjectKey] = aSequence.SubjectId,
            ["duration"] = aSequence.Duration,
            ["resolution"] = aSequence.Resolution,
            ["joinedAssetId"] = aSequence.JoinedAssetId,
            ["createdUtc"] = aSequence.CreatedUtc,
            ["finishedUtc"] = aSequence.FinishedUtc,
            ["shots"] = aSequence.Shots.Select(aShot => new Dictionary<string, object?>
            {
                ["index"] = aShot.ShotIndex,
                [vSubjectKey] = aSequence.SubjectId,
                ["prompt"] = aShot.Prompt,
                ["jobId"] = aShot.JobId,
                ["state"] = aShot.State,
                ["startFrameAssetId"] = aShot.StartFrameAssetId,
                ["assetId"] = aShot.ResultAssetId
            }).ToList()
        };
        return vView;
    }

    private static string? ResolveUserId(ClaimsPrincipal aUser)
        => aUser.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? aUser.FindFirst("sub")?.Value;
}
