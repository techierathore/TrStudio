using System.Security.Claims;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Services;

namespace TrStudioApi.Endpoints;

/// <summary>
/// Endpoint mapping for synthetic characters and their RAG-backed bible (FN-036/RAG-004/BRD-27/55):
/// owner-scoped CRUD at <c>/api/v1/characters</c> and per-character bible add/list/delete/retrieve at
/// <c>/api/v1/characters/{id}/bible</c>. Every handler resolves the caller's AppManager user id and
/// passes it into the owner-filtered services (BRD-81).
/// </summary>
public static class CharacterEndpoints
{
    /// <summary>
    /// Maps the character + bible endpoints onto the application pipeline.
    /// </summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapCharacterEndpoints(this WebApplication aApp)
    {
        var vCharacters = aApp.MapGroup(ApiRoutes.Characters).WithTags("Characters");
        vCharacters.MapPost("", CreateCharacterAsync).WithName("CreateCharacter");
        vCharacters.MapGet("", ListCharactersAsync).WithName("ListCharacters");
        vCharacters.MapGet("{aCharacterId:guid}", GetCharacterAsync).WithName("GetCharacter");
        vCharacters.MapPut("{aCharacterId:guid}", UpdateCharacterAsync).WithName("UpdateCharacter");
        vCharacters.MapDelete("{aCharacterId:guid}", DeleteCharacterAsync).WithName("DeleteCharacter");

        vCharacters.MapPost("{aCharacterId:guid}/bible", AddBibleEntryAsync).WithName("AddCharacterBibleEntry");
        vCharacters.MapGet("{aCharacterId:guid}/bible", ListBibleEntriesAsync).WithName("ListCharacterBibleEntries");
        vCharacters.MapGet("{aCharacterId:guid}/bible/retrieve", RetrieveBibleAsync).WithName("RetrieveCharacterBible");
        vCharacters.MapDelete("bible/{aEntryId:guid}", DeleteBibleEntryAsync).WithName("DeleteCharacterBibleEntry");
        return aApp;
    }

    /// <summary>FN-036: creates a character and mints its locked identity.</summary>
    private static async Task<IResult> CreateCharacterAsync(
        CharacterUpsertRequest aRequest,
        ClaimsPrincipal aUser,
        ICharacterService aService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var vResponse = await aService.CreateAsync(aRequest, vUserId, aCancellationToken);
            return Results.Created($"{ApiRoutes.Characters}/{vResponse.CharacterId}", vResponse);
        }
        catch (ArgumentException vException)
        {
            return Results.BadRequest(new { error = vException.Message });
        }
    }

    /// <summary>FN-036: lists the caller's characters.</summary>
    private static async Task<IResult> ListCharactersAsync(
        ClaimsPrincipal aUser,
        ICharacterService aService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await aService.ListAsync(vUserId, aCancellationToken));
    }

    /// <summary>FN-036: gets one character (owner-filtered).</summary>
    private static async Task<IResult> GetCharacterAsync(
        Guid aCharacterId,
        ClaimsPrincipal aUser,
        ICharacterService aService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vResponse = await aService.GetAsync(aCharacterId, vUserId, aCancellationToken);
        return vResponse is null ? Results.NotFound() : Results.Ok(vResponse);
    }

    /// <summary>FN-036: updates a character, preserving the locked identity.</summary>
    private static async Task<IResult> UpdateCharacterAsync(
        Guid aCharacterId,
        CharacterUpsertRequest aRequest,
        ClaimsPrincipal aUser,
        ICharacterService aService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var vResponse = await aService.UpdateAsync(aCharacterId, aRequest, vUserId, aCancellationToken);
            return vResponse is null ? Results.NotFound() : Results.Ok(vResponse);
        }
        catch (ArgumentException vException)
        {
            return Results.BadRequest(new { error = vException.Message });
        }
    }

    /// <summary>FN-036: deletes a character (owner-filtered).</summary>
    private static async Task<IResult> DeleteCharacterAsync(
        Guid aCharacterId,
        ClaimsPrincipal aUser,
        ICharacterService aService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vDeleted = await aService.DeleteAsync(aCharacterId, vUserId, aCancellationToken);
        return vDeleted ? Results.NoContent() : Results.NotFound();
    }

    /// <summary>RAG-004: adds a persona-notes entry to a character's bible.</summary>
    private static async Task<IResult> AddBibleEntryAsync(
        Guid aCharacterId,
        CharacterBibleEntryRequest aRequest,
        ClaimsPrincipal aUser,
        ICharacterBibleService aService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        try
        {
            var vResponse = await aService.AddEntryAsync(aCharacterId, aRequest, vUserId, aCancellationToken);
            return vResponse is null ? Results.NotFound() : Results.Ok(vResponse);
        }
        catch (ArgumentException vException)
        {
            return Results.BadRequest(new { error = vException.Message });
        }
    }

    /// <summary>RAG-004: lists a character's persisted bible entries.</summary>
    private static async Task<IResult> ListBibleEntriesAsync(
        Guid aCharacterId,
        ClaimsPrincipal aUser,
        ICharacterBibleService aService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vResponse = await aService.ListEntriesAsync(aCharacterId, vUserId, aCancellationToken);
        return vResponse is null ? Results.NotFound() : Results.Ok(vResponse);
    }

    /// <summary>RAG-004: retrieves the persona notes most relevant to a script-assist query (degrades to keyword).</summary>
    private static async Task<IResult> RetrieveBibleAsync(
        Guid aCharacterId,
        ClaimsPrincipal aUser,
        ICharacterBibleService aService,
        CancellationToken aCancellationToken,
        string? aQuery = null,
        int aTopK = 5)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vResponse = await aService.RetrieveAsync(aCharacterId, aQuery ?? string.Empty, vUserId, aTopK, aCancellationToken);
        return vResponse is null ? Results.NotFound() : Results.Ok(vResponse);
    }

    /// <summary>RAG-004: deletes a bible entry (owner-filtered).</summary>
    private static async Task<IResult> DeleteBibleEntryAsync(
        Guid aEntryId,
        ClaimsPrincipal aUser,
        ICharacterBibleService aService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vDeleted = await aService.DeleteEntryAsync(aEntryId, vUserId, aCancellationToken);
        return vDeleted ? Results.NoContent() : Results.NotFound();
    }

    private static string? ResolveUserId(ClaimsPrincipal aUser)
        => aUser.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? aUser.FindFirst("sub")?.Value;
}
