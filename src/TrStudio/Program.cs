using TrBlazeUI.Components.Toast;
using TrBlazeUI.Primitives.Extensions;
using TrStudio.Components;
using TrStudioUI.Auth;
using TrStudioUI.Navigation;

var vBuilder = WebApplication.CreateBuilder(args);

vBuilder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// TrBlazeUI services required by the shared TrStudioUI component library.
vBuilder.Services.AddTrBlazeUIPrimitives();
vBuilder.Services.AddScoped<ToastService>();

// Unified role-based app-shell navigation (REQ-UI-005 / REQ-UI-080 / ADR-012): both surfaces
// (creator studios + management) are registered; AppShellNavigation selects the active one from
// the signed-in user's role and gates the /admin/* routes client-side (REQ-FN-072). TrStudioAdmin
// is retired — this one app now hosts both surfaces.
vBuilder.Services.AddTrStudioRoleBasedNavigation(
    TrStudioNavSets.CreateCreatorNavigation(),
    TrStudioNavSets.CreateManagementNavigation());

// Auth session (encrypted ProtectedSessionStorage) + the bearer-attaching message handler so every
// per-user TrStudioApi call carries the signed-in user's token instead of going out anonymous.
vBuilder.Services.AddTrStudioAuthSession();

// Named HTTP client for TrStudioApi; base address comes from configuration (TrStudioApi:BaseUrl).
// The AuthTokenHandler stamps Authorization: Bearer from the circuit-scoped IAuthSession.
vBuilder.Services.AddHttpClient("TrStudioApi", (aServiceProvider, aClient) =>
{
    var vConfiguration = aServiceProvider.GetRequiredService<IConfiguration>();
    var vBaseUrl = vConfiguration["TrStudioApi:BaseUrl"] ?? "http://localhost:5100";
    aClient.BaseAddress = new Uri(vBaseUrl);
})
.AddHttpMessageHandler<AuthTokenHandler>();

var vApp = vBuilder.Build();

if (!vApp.Environment.IsDevelopment())
{
    vApp.UseExceptionHandler("/Error", createScopeForErrors: true);
    vApp.UseHsts();
}

vApp.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

vApp.UseAntiforgery();

vApp.MapStaticAssets();
vApp.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(TrStudioUI.Layout.MainLayout).Assembly);

vApp.Run();
