using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using Serilog;
using TrBlazeUI.Components.Toast;
using TrBlazeUI.Primitives.Extensions;
using TrStudio.Auth;
using TrStudio.Dev;
using TrStudio.Engine;
using TrStudio.InProcess;
using TrStudioUI.Auth;
using TrStudioUI.Navigation;

namespace TrStudio;

/// <summary>
/// Builds the TrStudio desktop app (phase 3, ADR-015 to ADR-020): one process holding the
/// <c>TrStudioUI</c> screens, the engine, the local SQLite store and the in-app job runner. There is no
/// API process and no web server: the screens' named <c>TrStudioApi</c> client is answered in process
/// by <see cref="InProcessEngineHandler"/>. The data lives in the signed-in user's own app data folder;
/// sign-in uses the AppManager application id and key built into the app. Nothing is read from an
/// environment variable and no user edits a settings file.
/// </summary>
public static class MauiProgram
{
    /// <summary>Creates the MAUI app.</summary>
    /// <returns>The app.</returns>
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

        vBuilder.Services.AddTrBlazeUIPrimitives();
        vBuilder.Services.AddScoped<ToastService>();

        var vSettings = DesktopSettings.Load();
        vBuilder.Configuration.AddInMemoryCollection(vSettings.ToConfiguration());

        // The user's own data folder (Mac: the app's container; Windows: the user's local app data).
        var vDataFolder = Path.Combine(FileSystem.AppDataDirectory, "TrStudio");
        var vLoggerFactory = CreateFileLogging(vDataFolder);
        vBuilder.Logging.AddSerilog(dispose: false);

        // The engine inside the app: create it, then prepare the local store and start the jobs.
        var vHost = EngineHost.Create(new EngineHostOptions
        {
            DataFolder = vDataFolder,
            SignIn = AppManagerBuildSettings.Load(),
            KeyProtector = new SecureStorageKeyProtector(),
            LoggerFactory = vLoggerFactory,
#if MACCATALYST
            // REQ-NFR-023: the Mac app joins clips with Apple's AVFoundation (ffmpeg cannot be relied on in the sandbox).
            ConfigureServices = aServices => Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.Replace(aServices, ServiceDescriptor.Singleton<TrStudio.Engine.Domain.Video.IClipJoiner, TrStudio.Platforms.MacCatalyst.AvFoundationClipJoiner>())
#endif
        });
        Task.Run(() => vHost.StartAsync()).GetAwaiter().GetResult();
        vBuilder.Services.AddSingleton(vHost);
        DesktopDevSession.Host = vHost;

        vBuilder.Services.AddSingleton<IAuthSession, MauiAuthSession>();
        vBuilder.Services.AddTransient<MauiAuthTokenHandler>();

        vBuilder.Services.AddHttpClient("TrStudioApi", aClient => aClient.BaseAddress = InProcessEngineHandler.BaseAddress)
            .ConfigurePrimaryHttpMessageHandler(aProvider => new InProcessEngineHandler(aProvider.GetRequiredService<EngineHost>()))
            .AddHttpMessageHandler<MauiAuthTokenHandler>();

        vBuilder.Services.AddTrStudioRoleBasedNavigation(
            TrStudioNavSets.CreateCreatorNavigation(),
            TrStudioNavSets.CreateManagementNavigation());

#if DEBUG
        vBuilder.Services.AddBlazorWebViewDeveloperTools();
        vBuilder.Logging.AddDebug();
#endif

        var vApp = vBuilder.Build();

        // DEBUG developer session (REQ-NFR-015): opens already signed in with a chosen role, registered
        // with the in-process engine (no signed token, no signing key).
        if (DesktopDevSession.IsEnabled)
        {
            var vRequestedRole = vSettings.DevRole;
            if (!string.IsNullOrWhiteSpace(vRequestedRole))
            {
                var vRole = DesktopDevSession.ParseRole(vRequestedRole);
                if (vApp.Services.GetService<IAuthSession>() is MauiAuthSession vSession)
                {
                    vSession.Seed(DesktopDevSession.BuildDevState(vRole));
                }

                var vStartPath = vSettings.DevStartPath;
                DesktopDevSession.RequestedNavPath = string.IsNullOrWhiteSpace(vStartPath)
                    ? DesktopDevSession.LandingFor(vRole)
                    : vStartPath;
            }
        }

        return vApp;
    }

    private static ILoggerFactory CreateFileLogging(string aDataFolder)
    {
        // Logs go under the user's data folder, never the repository; no key is ever logged.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(aDataFolder, "logs", "trstudio-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();
        return LoggerFactory.Create(aLogging => aLogging.AddSerilog(Log.Logger, dispose: false));
    }
}
