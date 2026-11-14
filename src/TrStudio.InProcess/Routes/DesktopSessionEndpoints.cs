using TrStudio.InProcess.Hosting;
using TrStudio.Models;

namespace TrStudio.InProcess.Routes;

/// <summary>
/// The desktop app's own session routes (phase 3, ADR-019): whether the build carries its sign-in
/// settings (REQ-UI-098), where the user's data lives (REQ-UI-101) and signing out (REQ-FN-088).
/// </summary>
public static class DesktopSessionEndpoints
{
    /// <summary>The sign-in settings route.</summary>
    public const string SignInSettingsRoute = ApiRoutes.Auth + "/sign-in-settings";

    /// <summary>The sign-out route.</summary>
    public const string SignOutRoute = ApiRoutes.Auth + "/logout";

    /// <summary>The local store route.</summary>
    public const string LocalStoreRoute = "/api/v1/local-store";

    /// <summary>The words shown when the build has no sign-in settings.</summary>
    public const string MissingSettingsMessage =
        "This copy of TrStudio was built without its sign-in settings, so it cannot sign you in. Download it again or ask the person who built it.";

    /// <summary>The words shown when AppManager cannot be reached.</summary>
    public const string UnreachableMessage =
        "The sign-in service (AppManager) cannot be reached. Check your internet connection and try again.";

    /// <summary>Maps the routes.</summary>
    /// <param name="aApp">The route table.</param>
    /// <returns>The same table.</returns>
    public static WebApplication MapDesktopSessionEndpoints(this WebApplication aApp)
    {
        aApp.MapGet(SignInSettingsRoute, (SignInSettings aSettings) => Results.Ok(new
        {
            configured = aSettings.IsConfigured,
            message = aSettings.IsConfigured ? null : MissingSettingsMessage
        })).AllowAnonymous();

        aApp.MapPost(SignOutRoute, (HttpContext aContext, LocalSessionStore aSessions) =>
        {
            const string BearerPrefix = "Bearer ";
            var vHeader = aContext.Request.Headers.Authorization;
            if (vHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                aSessions.SignOut(vHeader[BearerPrefix.Length..].Trim());
            }

            return Results.NoContent();
        }).AllowAnonymous();

        aApp.MapGet(LocalStoreRoute, (Microsoft.Extensions.Configuration.IConfiguration aConfiguration) => Results.Ok(new
        {
            dataFolder = aConfiguration[TrStudio.Engine.Infrastructure.Data.LocalStoreOptions.DataFolderKey],
            ready = true
        })).AllowAnonymous();

        return aApp;
    }
}
