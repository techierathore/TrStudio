using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrBlazeUI.Components.Toast;
using TrBlazeUI.Primitives.Extensions;
using TrStudio.Desktop.Auth;
using TrStudio.Desktop.Dev;
using TrStudioUI.Auth;
using TrStudioUI.Navigation;

namespace TrStudio.Desktop;

/// <summary>
/// Composition root for the MAUI Blazor Hybrid desktop head. Wires the BlazorWebView and the SAME
/// services the shared TrStudioUI RCL expects from the Blazor Web host — role-based navigation, the
/// TrBlazeUI primitives/toast services, and a named TrStudioApi HttpClient with a bearer handler —
/// but backs the session with a Hybrid <see cref="MauiAuthSession"/> (SecureStorage) instead of the
/// web head's Blazor-Server ProtectedSessionStorage. The result: one component set, two heads.
/// </summary>
public static class MauiProgram
{
    /// <summary>Builds and configures the MAUI application.</summary>
    /// <returns>The configured <see cref="MauiApp"/>.</returns>
    public static MauiApp CreateMauiApp()
    {
        var vBuilder = MauiApp.CreateBuilder();
        vBuilder
            .UseMauiApp<App>()
            .ConfigureFonts(aFonts =>
            {
                aFonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        vBuilder.Services.AddMauiBlazorWebView();

        // TrBlazeUI services the shared TrStudioUI components require (same as the web host).
        vBuilder.Services.AddTrBlazeUIPrimitives();
        vBuilder.Services.AddScoped<ToastService>();

        // Hybrid auth session: SecureStorage-backed, registered as a SINGLETON so the WebView-scoped
        // components and the IHttpClientFactory-scoped bearer handler share one process-wide session
        // (the web head bridged Blazor circuit scopes instead — no analogue in a Hybrid host).
        vBuilder.Services.AddSingleton<IAuthSession, MauiAuthSession>();
        vBuilder.Services.AddTransient<MauiAuthTokenHandler>();

        // Named TrStudioApi HttpClient — BaseAddress = the API reachable from the desktop process
        // (dev default http://localhost:5102; override with the TrStudioApiBaseUrl env var). The
        // bearer handler stamps the signed-in user's token so owner-scoped screen calls authenticate.
        var vApiBaseUrl = Environment.GetEnvironmentVariable("TrStudioApiBaseUrl");
        if (string.IsNullOrWhiteSpace(vApiBaseUrl))
        {
            vApiBaseUrl = "http://localhost:5102";
        }

        vBuilder.Services.AddHttpClient("TrStudioApi", aClient => aClient.BaseAddress = new Uri(vApiBaseUrl))
            .AddHttpMessageHandler<MauiAuthTokenHandler>();

        // Unified role-based shell (ADR-012): both surfaces registered; the shell selects the active
        // one from the signed-in role and gates /admin/* — identical behaviour to the web head.
        vBuilder.Services.AddTrStudioRoleBasedNavigation(
            TrStudioNavSets.CreateCreatorNavigation(),
            TrStudioNavSets.CreateManagementNavigation());

#if DEBUG
        vBuilder.Services.AddBlazorWebViewDeveloperTools();
        vBuilder.Logging.AddDebug();
#endif

        var vApp = vBuilder.Build();

        // Dev-only: a native-window smoke can boot straight into a role via the TrStudioDevRole env
        // var (Creator/Operator/Owner), optionally deep-linking a route via TrStudioDevStartPath. The
        // session is pre-seeded SYNCHRONOUSLY here — before the WebView's first render — so the shell
        // initialises on the correct role and the initializer only needs a client-side nav (no reload).
        // Read directly: a MAUI bootstrap launch flag, not the API's IConfiguration surface; DEBUG-only.
        if (DesktopDevSession.IsEnabled)
        {
            var vRequestedRole = Environment.GetEnvironmentVariable("TrStudioDevRole");
            if (!string.IsNullOrWhiteSpace(vRequestedRole))
            {
                var vRole = DesktopDevSession.ParseRole(vRequestedRole);
                if (vApp.Services.GetService<IAuthSession>() is MauiAuthSession vSession)
                {
                    vSession.Seed(DesktopDevSession.BuildDevState(vRole));
                }

                var vStartPath = Environment.GetEnvironmentVariable("TrStudioDevStartPath");
                DesktopDevSession.RequestedNavPath = string.IsNullOrWhiteSpace(vStartPath)
                    ? DesktopDevSession.LandingFor(vRole)
                    : vStartPath;
            }
        }

        return vApp;
    }
}
