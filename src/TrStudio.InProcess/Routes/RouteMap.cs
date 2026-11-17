using TrStudio.InProcess.Hosting;

namespace TrStudio.InProcess.Routes;

/// <summary>
/// Maps every in-process route (phase 3, ADR-015), in the order the API host mapped its endpoints,
/// plus the desktop's own sign-in routes.
/// </summary>
public static class RouteMap
{
    /// <summary>Maps all routes onto the table.</summary>
    /// <param name="aApp">The route table.</param>
    public static void MapAll(WebApplication aApp)
    {
        aApp.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
        aApp.MapSpineEndpoints();
        aApp.MapRegistryEndpoints();
        aApp.MapVeniceEndpoints();
        aApp.MapSetupEndpoints();
        aApp.MapIdentityEndpoints();
        aApp.MapDesktopSessionEndpoints();
        aApp.MapImageEndpoints();
        aApp.MapWorkspaceEndpoints();
        aApp.MapPhase3Endpoints();
        aApp.MapVoiceEndpoints();
        aApp.MapRagEndpoints();
        aApp.MapCharacterEndpoints();
        aApp.MapAvatarEndpoints();
        aApp.MapSequenceEndpoints();
        aApp.MapTwinEndpoints();
        aApp.MapVideoEndpoints();
        aApp.MapSafetyEndpoints();
        aApp.MapComfyEndpoints();
        aApp.MapPhase9Endpoints();
        aApp.MapPromptChatEndpoints();
        aApp.MapDesktopProviderEndpoints();
    }
}
