using System.Security.Claims;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Services;

namespace TrStudioApi.Endpoints;

/// <summary>
/// Endpoint mapping for the text-RAG features (RAG-001/002/003/BRD-53/54/56): script generation from
/// a brief (<c>/api/v1/rag/script</c>), caption/subtitle text from a transcript
/// (<c>/api/v1/rag/captions</c>) and the per-user text-LLM token-usage rollup
/// (<c>/api/v1/rag/usage</c>). Generation is driven through TechieRag and degrades gracefully when no
/// LLM is configured; token usage is attributed to the caller and feeds the LlmText spend channel.
/// </summary>
public static class RagEndpoints
{
    /// <summary>
    /// Maps the RAG endpoints onto the application pipeline.
    /// </summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapRagEndpoints(this WebApplication aApp)
    {
        var vRag = aApp.MapGroup(ApiRoutes.Rag).WithTags("Rag");
        vRag.MapPost("script", GenerateScriptAsync).WithName("GenerateScript");
        vRag.MapPost("captions", GenerateCaptionsAsync).WithName("GenerateCaptions");
        vRag.MapGet("usage", GetUsageAsync).WithName("GetLlmUsage");
        return aApp;
    }

    /// <summary>RAG-001: generates/expands a script from a brief; degrades cleanly without an LLM.</summary>
    private static async Task<IResult> GenerateScriptAsync(
        ScriptGenerationRequest aRequest,
        ClaimsPrincipal aUser,
        ITextGenerationService aService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vResponse = await aService.GenerateScriptAsync(aRequest, vUserId, aCancellationToken);
        return Results.Ok(vResponse);
    }

    /// <summary>RAG-002: produces caption/subtitle text from a Whisper transcript; degrades cleanly.</summary>
    private static async Task<IResult> GenerateCaptionsAsync(
        CaptionGenerationRequest aRequest,
        ClaimsPrincipal aUser,
        ITextGenerationService aService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vResponse = await aService.GenerateCaptionsAsync(aRequest, vUserId, aCancellationToken);
        return Results.Ok(vResponse);
    }

    /// <summary>RAG-003: returns the caller's text-LLM token-usage rollup (feeds the LlmText spend channel).</summary>
    private static IResult GetUsageAsync(ClaimsPrincipal aUser, ILlmTokenTracker aTracker)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(aTracker.GetUsage(vUserId));
    }

    private static string? ResolveUserId(ClaimsPrincipal aUser)
        => aUser.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? aUser.FindFirst("sub")?.Value;
}
