using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TrStudio.Engine.Infrastructure.Identity;
using TrStudio.InProcess;
using TrStudio.InProcess.Hosting;
using TrStudio.InProcess.Routes;
using TrStudio.Models.Contracts;
using TrStudio.Models.Services;

namespace TrStudio.Tests.Unit.Desktop;

/// <summary>A clock fixed at one moment (moves a kept sign-in's start into the past).</summary>
internal sealed class FixedClock : TimeProvider
{
    private readonly DateTimeOffset objNow;

    public FixedClock(DateTimeOffset aNow) => objNow = aNow;

    public override DateTimeOffset GetUtcNow() => objNow;
}

/// <summary>An AppManager that answers the public key and login, and refuses or cannot be reached on refresh.</summary>
internal sealed class RefusingAppManager : HttpMessageHandler
{
    private readonly FakeAppManager objInner = new();

    public bool Unreachable { get; init; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
    {
        if (!aRequest.RequestUri!.AbsolutePath.EndsWith("AuthSvc/refresh", StringComparison.OrdinalIgnoreCase))
        {
            return await new HttpMessageInvoker(objInner).SendAsync(aRequest, aCancellationToken);
        }

        if (Unreachable)
        {
            throw new HttpRequestException("connection refused");
        }

        var vJson = JsonSerializer.Serialize(new { success = false, error = "REVOKED_REFRESH_TOKEN", message = "Refresh token has been revoked" });
        return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent(vJson, Encoding.UTF8, "application/json") };
    }
}

/// <summary>
/// "Remember me" on the sign-in screen (owner decision 6B, ADR-021): REQ-FN-099, REQ-FN-100, REQ-FN-101,
/// REQ-UI-105, REQ-UI-106. The engine runs on a temporary SQLite folder with a fake AppManager; no real
/// account is used.
/// </summary>
public class RememberMeTests
{
    private static readonly SignInSettings Settings = new("https://appmanager.test.invalid/", 7, "ak_test_desktop_key");

    private static Task<DesktopEngine> StartAsync(HttpMessageHandler aAppManager, string? aFolder = null, CountingKeyProtector? aProtector = null)
        => DesktopEngine.StartAsync(aFolder: aFolder, aSignIn: Settings, aProtector: aProtector, aConfigure: aServices =>
            aServices.AddHttpClient<IAppManagerClient, AppManagerClient>().ConfigurePrimaryHttpMessageHandler(() => aAppManager));

    private static async Task<LoginResponse> SignInAsync(DesktopEngine aEngine, bool aRememberMe)
    {
        using var vClient = aEngine.Client();
        var vResponse = await vClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Email = "creator@test.local", Password = "pass-word-1", RememberMe = aRememberMe });
        Assert.Equal(HttpStatusCode.OK, vResponse.StatusCode);
        return (await vResponse.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private static async Task<(HttpStatusCode Status, string Body)> ResumeAsync(DesktopEngine aEngine)
    {
        using var vClient = aEngine.Client();
        var vResponse = await vClient.PostAsync("/api/v1/auth/resume", null);
        return (vResponse.StatusCode, await vResponse.Content.ReadAsStringAsync());
    }

    /// <summary>REQ-FN-099: ticked keeps the refresh token, encrypted for this user, for 30 days.</summary>
    [Fact(DisplayName = "REQ-FN-099 TickedSignInKeepsTheTokenProtectedFor30Days")]
    public async Task TickedSignInKeepsTheTokenProtectedFor30Days()
    {
        await using var vEngine = await StartAsync(new FakeAppManager());

        await SignInAsync(vEngine, aRememberMe: true);

        var vStore = vEngine.Host.RememberedSignIn;
        Assert.True(vStore.HasKept);
        var vBytes = await File.ReadAllBytesAsync(vStore.FilePath);
        Assert.DoesNotContain("am-refresh-1", Encoding.UTF8.GetString(vBytes), StringComparison.Ordinal);
        Assert.DoesNotContain("creator@test.local", Encoding.UTF8.GetString(vBytes), StringComparison.Ordinal);
        var vKept = Assert.IsType<RememberedSignIn>(vStore.Read());
        Assert.Equal("am-refresh-1", vKept.RefreshToken);
        Assert.InRange(vKept.KeptUntil, DateTimeOffset.UtcNow.AddDays(30).AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(30).AddMinutes(5));
        Assert.True(vEngine.Protector.Calls > 0, "the unlock key comes from the operating system's protection (ADR-020)");
    }

    /// <summary>REQ-FN-100: the next open signs in with the kept token and asks for nothing.</summary>
    [Fact(DisplayName = "REQ-FN-100 SecondOpenSignsInWithoutAsking")]
    public async Task SecondOpenSignsInWithoutAsking()
    {
        var vFolder = DesktopEngine.NewFolder();
        var vProtector = new CountingKeyProtector();
        await using (var vFirst = await StartAsync(new FakeAppManager(), vFolder, vProtector))
        {
            vFirst.KeepFolder = true;
            await SignInAsync(vFirst, aRememberMe: true);
        }

        var vSecondAppManager = new FakeAppManager();
        await using var vSecond = await StartAsync(vSecondAppManager, vFolder, vProtector);

        var (vStatus, vBody) = await ResumeAsync(vSecond);

        Assert.Equal(HttpStatusCode.OK, vStatus);
        Assert.Contains("am-token-1", vBody, StringComparison.Ordinal);
        Assert.Equal(1, vSecond.Host.Sessions.Count);
        Assert.DoesNotContain(vSecondAppManager.Requests, aRequest => aRequest.RequestUri!.AbsolutePath.EndsWith("AuthSvc/login", StringComparison.Ordinal));
        Assert.Contains(vSecondAppManager.Requests, aRequest => aRequest.RequestUri!.AbsolutePath.EndsWith("AuthSvc/refresh", StringComparison.Ordinal));
        Assert.True(vSecond.Host.RememberedSignIn.HasKept, "the renewed token is kept for the rest of the 30 days");
    }

    /// <summary>REQ-FN-101: after 30 days the kept sign-in is removed and the user is asked to sign in, in plain words.</summary>
    [Fact(DisplayName = "REQ-FN-101 After30DaysTheUserSignsInAgain")]
    public async Task After30DaysTheUserSignsInAgain()
    {
        var vAppManager = new FakeAppManager();
        await using var vEngine = await StartAsync(vAppManager);
        var vOld = new RememberedSignInStore(vEngine.Folder, vEngine.Protector, new FixedClock(DateTimeOffset.UtcNow.AddDays(-31)));
        vOld.Remember("am-refresh-old", "creator@test.local");

        var (vStatus, vBody) = await ResumeAsync(vEngine);

        Assert.Equal(HttpStatusCode.Unauthorized, vStatus);
        Assert.Contains("SIGN_IN_ENDED", vBody, StringComparison.Ordinal);
        Assert.Contains("30 days", vBody, StringComparison.Ordinal);
        Assert.False(vEngine.Host.RememberedSignIn.HasKept);
        Assert.Empty(vAppManager.Requests);
        Assert.Equal(0, vEngine.Host.Sessions.Count);
    }

    /// <summary>REQ-FN-101: a token AppManager refuses or has revoked is removed and the user signs in again.</summary>
    [Fact(DisplayName = "REQ-FN-101 RefusedTokenAsksForSignInAgain")]
    public async Task RefusedTokenAsksForSignInAgain()
    {
        await using var vEngine = await StartAsync(new RefusingAppManager());
        await SignInAsync(vEngine, aRememberMe: true);
        vEngine.Host.Sessions.SignOut("am-token-1");

        var (vStatus, vBody) = await ResumeAsync(vEngine);

        Assert.Equal(HttpStatusCode.Unauthorized, vStatus);
        Assert.Contains(DesktopSessionEndpoints.KeptSignInRefusedMessage, vBody, StringComparison.Ordinal);
        Assert.False(vEngine.Host.RememberedSignIn.HasKept);
        Assert.Equal(0, vEngine.Host.Sessions.Count);
    }

    /// <summary>REQ-FN-101: an unreachable AppManager keeps the kept sign-in for the next open.</summary>
    [Fact(DisplayName = "REQ-FN-101 UnreachableServiceKeepsTheKeptSignIn")]
    public async Task UnreachableServiceKeepsTheKeptSignIn()
    {
        await using var vEngine = await StartAsync(new RefusingAppManager { Unreachable = true });
        await SignInAsync(vEngine, aRememberMe: true);

        var (vStatus, vBody) = await ResumeAsync(vEngine);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, vStatus);
        Assert.Contains("SIGN_IN_UNREACHABLE", vBody, StringComparison.Ordinal);
        Assert.True(vEngine.Host.RememberedSignIn.HasKept);
    }

    /// <summary>REQ-UI-105: not ticked keeps nothing, and forgets an older kept sign-in.</summary>
    [Fact(DisplayName = "REQ-UI-105 UntickedSignInKeepsNothing")]
    public async Task UntickedSignInKeepsNothing()
    {
        await using var vEngine = await StartAsync(new FakeAppManager());

        await SignInAsync(vEngine, aRememberMe: true);
        Assert.True(vEngine.Host.RememberedSignIn.HasKept);
        await SignInAsync(vEngine, aRememberMe: false);

        Assert.False(vEngine.Host.RememberedSignIn.HasKept);
        Assert.False(File.Exists(Path.Combine(vEngine.Folder, RememberedSignInStore.FileName)));
        var (vStatus, _) = await ResumeAsync(vEngine);
        Assert.Equal(HttpStatusCode.NoContent, vStatus);
    }

    /// <summary>REQ-UI-106: signing out always removes what was kept.</summary>
    [Fact(DisplayName = "REQ-UI-106 SignOutRemovesTheKeptSignIn")]
    public async Task SignOutRemovesTheKeptSignIn()
    {
        await using var vEngine = await StartAsync(new FakeAppManager());
        var vSession = await SignInAsync(vEngine, aRememberMe: true);
        Assert.True(vEngine.Host.RememberedSignIn.HasKept);

        using var vClient = vEngine.Client(vSession.Token);
        var vResponse = await vClient.PostAsync(DesktopSessionEndpoints.SignOutRoute, null);

        Assert.Equal(HttpStatusCode.NoContent, vResponse.StatusCode);
        Assert.False(vEngine.Host.RememberedSignIn.HasKept);
        Assert.Equal(0, vEngine.Host.Sessions.Count);
        var (vStatus, _) = await ResumeAsync(vEngine);
        Assert.Equal(HttpStatusCode.NoContent, vStatus);
    }

    /// <summary>REQ-FN-099: a kept file another user's unlock key cannot open is forgotten, never used.</summary>
    [Fact(DisplayName = "REQ-FN-099 AnotherUsersKeyCannotOpenTheKeptSignIn")]
    public void AnotherUsersKeyCannotOpenTheKeptSignIn()
    {
        var vFolder = DesktopEngine.NewFolder();
        try
        {
            new RememberedSignInStore(vFolder, new CountingKeyProtector()).Remember("am-refresh-1", "creator@test.local");
            var vOther = new RememberedSignInStore(vFolder, new CountingKeyProtector());

            Assert.Null(vOther.Read());
            Assert.False(vOther.HasKept);
        }
        finally
        {
            if (Directory.Exists(vFolder))
            {
                Directory.Delete(vFolder, recursive: true);
            }
        }
    }
}
