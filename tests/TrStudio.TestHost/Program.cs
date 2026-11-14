using TrBlazeUI.Components.Toast;
using TrBlazeUI.Primitives.Extensions;
using TrStudio.Components;
using TrStudio.InProcess;
using TrStudio.Models.Enums;
using TrStudio.TestHost;
using TrStudioUI.Auth;
using TrStudioUI.Navigation;

// TEST TOOL ONLY — never shipped (phase 3, REQ-NFR-020). The same TrStudioUI screens as the desktop
// app, over the same in-process engine on SQLite, served to a headless browser. Settings come from
// the command line and the test settings files beside the tests, never environment variables.
var vBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = "Development"
});

vBuilder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

vBuilder.Services.AddTrBlazeUIPrimitives();
vBuilder.Services.AddScoped<ToastService>();

vBuilder.Services.AddTrStudioRoleBasedNavigation(
    TrStudioNavSets.CreateCreatorNavigation(),
    TrStudioNavSets.CreateManagementNavigation());

vBuilder.Services.AddTrStudioAuthSession();

// The engine, in this process, on a fresh SQLite data folder per run unless one is named.
var vDataFolder = vBuilder.Configuration["TestHost:DataFolder"];
if (string.IsNullOrWhiteSpace(vDataFolder))
{
    vDataFolder = Path.Combine(Path.GetTempPath(), "trstudio-testhost", Guid.NewGuid().ToString("N"));
}

var vVerifyFolder = TestHostSettings.FindVerifyFolder(vBuilder.Environment.ContentRootPath);
var vFileSignIn = TestHostSettings.SignInFor(vBuilder.Configuration["TestHost:SignIn"], vVerifyFolder);
var vSignIn = new SignInModeHolder(vFileSignIn);
var vHost = EngineHost.Create(new EngineHostOptions
{
    DataFolder = vDataFolder,
    SignIn = vFileSignIn,
    SignInSource = () => vSignIn.Current,
    KeyProtector = new TestKeyProtector(),
    // Without real test settings, AppManager is pointed at a closed local port, so "unreachable" is real.
    ExtraSettings = vFileSignIn.IsConfigured
        ? new Dictionary<string, string?>()
        : new Dictionary<string, string?> { ["AppManager:BaseUrl"] = "http://127.0.0.1:9/" }
});
await vHost.StartAsync();
vBuilder.Services.AddSingleton(vHost);

vBuilder.Services.AddHttpClient("TrStudioApi", aClient => aClient.BaseAddress = InProcessEngineHandler.BaseAddress)
    .ConfigurePrimaryHttpMessageHandler(aProvider => new InProcessEngineHandler(aProvider.GetRequiredService<EngineHost>()))
    .AddHttpMessageHandler<AuthTokenHandler>();

var vApp = vBuilder.Build();

vApp.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
vApp.UseAntiforgery();

// TEST ONLY: open a signed-in session of a role without AppManager, for the headless checks. The
// token goes back to the caller, which pastes it into the /dev/session page.
vApp.MapPost("/test/session", (string? role, EngineHost aHost) =>
{
    var vRole = Enum.TryParse<UserRole>(role, true, out var vParsed) ? vParsed : UserRole.Owner;
    var vToken = "test-" + Guid.NewGuid().ToString("N");
    aHost.Sessions.SignIn(vToken, $"test-{vRole}".ToLowerInvariant(), null, vRole);
    return Results.Ok(new { token = vToken, dataFolder = aHost.DataFolder });
});

// TEST ONLY: switch what the build's sign-in settings look like: "none" (a build without them),
// "unreachable" (an id and key, AppManager not reachable) or "file" (the test settings files).
vApp.MapPost("/test/sign-in-mode", (string? mode) =>
{
    vSignIn.Current = (mode ?? "file").ToLowerInvariant() switch
    {
        "none" => SignInSettings.None,
        "unreachable" => vFileSignIn.IsConfigured ? vFileSignIn : new SignInSettings("http://127.0.0.1:9/", 1, "test-only-key"),
        _ => vFileSignIn
    };
    return Results.Ok(new { configured = vSignIn.Current.IsConfigured, realSettings = vFileSignIn.IsConfigured });
});

vApp.MapStaticAssets();
vApp.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(TrStudioUI.Layout.MainLayout).Assembly);

await vApp.RunAsync();
await vHost.DisposeAsync();

/// <summary>TEST ONLY: the sign-in settings the test host currently shows.</summary>
internal sealed class SignInModeHolder
{
    public SignInModeHolder(SignInSettings aInitial) => Current = aInitial;

    public SignInSettings Current { get; set; }
}
