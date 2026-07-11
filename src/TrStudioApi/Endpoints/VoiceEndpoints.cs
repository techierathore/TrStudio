using System.Security.Claims;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Services;

namespace TrStudioApi.Endpoints;

/// <summary>
/// Endpoint mapping for the Voice vertical (FN-031..035/BRD-22..26): TTS synthesis
/// (<c>/api/v1/voice/synthesize</c>), the preset-voice catalog
/// (<c>/api/v1/voice/preset-voices</c>), the SAFE-03 consent-evidence recorder
/// (<c>/api/v1/voice/consent</c>), consent-gated voice cloning (<c>/api/v1/voice/clone</c>) and the
/// audio→avatar alignment handoff (<c>/api/v1/voice/avatar-handoff</c>). Every user-scoped handler
/// resolves the caller's AppManager user id and passes it into the owner-filtered services (BRD-81).
/// </summary>
public static class VoiceEndpoints
{
    /// <summary>
    /// Maps the Voice endpoints onto the application pipeline.
    /// </summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapVoiceEndpoints(this WebApplication aApp)
    {
        var vVoice = aApp.MapGroup(ApiRoutes.Voice).WithTags("Voice");
        vVoice.MapGet("preset-voices", ListPresetVoicesAsync).WithName("ListPresetVoices");
        vVoice.MapPost("synthesize", SynthesizeAsync).WithName("SynthesizeVoice");
        vVoice.MapPost("consent", RecordConsentAsync).WithName("RecordVoiceConsent");
        vVoice.MapPost("clone", CloneVoiceAsync).WithName("CloneVoice");
        vVoice.MapPost("avatar-handoff", BuildAvatarHandoffAsync).WithName("BuildVoiceAvatarHandoff");
        return aApp;
    }

    /// <summary>FN-031: lists preset voices (optionally filtered by language).</summary>
    private static IResult ListPresetVoicesAsync(
        IPresetVoiceCatalog aCatalog,
        IVoiceModelRouter aRouter,
        string? aLanguage = null)
    {
        var vVoices = string.IsNullOrWhiteSpace(aLanguage) ? aCatalog.GetAll() : aCatalog.GetByLanguage(aLanguage);
        var vResult = vVoices.Select(aVoice => ToResponse(aVoice, aRouter)).ToList();
        return Results.Ok(vResult);
    }

    /// <summary>FN-031/033/034: synthesizes speech; degrades cleanly when no voice model is installed.</summary>
    private static async Task<IResult> SynthesizeAsync(
        VoiceSynthesizeRequest aRequest,
        ClaimsPrincipal aUser,
        IVoiceSynthesizer aSynthesizer,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(aRequest.Text) || string.IsNullOrWhiteSpace(aRequest.VoiceId))
        {
            return Results.Problem(statusCode: 400, title: "VALIDATION_ERROR", detail: "Text and VoiceId are required.");
        }

        var vResponse = await aSynthesizer.SynthesizeAsync(aRequest, vUserId, aCancellationToken);
        return Results.Ok(vResponse);
    }

    /// <summary>FN-032 / SAFE-03: records recorded-consent evidence for a real subject.</summary>
    private static async Task<IResult> RecordConsentAsync(
        VoiceConsentRequest aRequest,
        ClaimsPrincipal aUser,
        IVoiceConsentService aConsentService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vResponse = await aConsentService.RecordConsentAsync(aRequest, vUserId, aCancellationToken);
        return vResponse.Recorded ? Results.Ok(vResponse) : Results.BadRequest(vResponse);
    }

    /// <summary>FN-032: requests a voice clone; real-person clones are blocked without consent (SAFE-03).</summary>
    private static async Task<IResult> CloneVoiceAsync(
        VoiceCloneRequest aRequest,
        ClaimsPrincipal aUser,
        IVoiceCloneService aCloneService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vResponse = await aCloneService.RequestCloneAsync(aRequest, vUserId, aCancellationToken);

        // A consent-gate block is a deliberate 403 (SAFE-03), not a server error.
        return vResponse.Blocked
            ? Results.Json(vResponse, statusCode: StatusCodes.Status403Forbidden)
            : Results.Ok(vResponse);
    }

    /// <summary>FN-035: builds the audio→avatar alignment handoff contract (Phase-5 pipeline input).</summary>
    private static async Task<IResult> BuildAvatarHandoffAsync(
        AvatarHandoffRequest aRequest,
        ClaimsPrincipal aUser,
        IAudioAvatarHandoff aHandoff,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vResponse = await aHandoff.BuildHandoffAsync(aRequest, vUserId, aCancellationToken);
        return Results.Ok(vResponse);
    }

    private static PresetVoiceResponse ToResponse(PresetVoice aVoice, IVoiceModelRouter aRouter) => new()
    {
        VoiceId = aVoice.VoiceId,
        DisplayName = aVoice.DisplayName,
        Language = aVoice.Language,
        Gender = aVoice.Gender,
        IsSynthetic = aVoice.IsSynthetic,
        RoutedModel = aRouter.Route(aVoice.Language).Model,
        SupportsEmotion = aVoice.SupportsEmotion,
        SupportsStyle = aVoice.SupportsStyle,
        SupportsSpeed = aVoice.SupportsSpeed,
        Emotions = aVoice.Emotions
    };

    private static string? ResolveUserId(ClaimsPrincipal aUser)
        => aUser.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? aUser.FindFirst("sub")?.Value;
}
