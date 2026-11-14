using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using TechieRag.Abstractions;
using TrStudio.Engine.Domain.Avatar;
using TrStudio.Engine.Domain.Rag;
using TrStudio.Engine.Domain.RunPod;
using TrStudio.Engine.Infrastructure.Identity;
using TrStudio.InProcess;
using TrStudio.InProcess.Routes;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Tests.Unit.Desktop;

/// <summary>A fake AppManager that records each request's headers and answers login and register.</summary>
internal sealed class FakeAppManager : HttpMessageHandler
{
    private static readonly RSA Rsa = RSA.Create(2048);

    public List<HttpRequestMessage> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
    {
        Requests.Add(aRequest);
        var vPath = aRequest.RequestUri!.AbsolutePath;
        object vData = vPath.EndsWith("public-key", StringComparison.OrdinalIgnoreCase)
            ? new { publicKey = Rsa.ExportSubjectPublicKeyInfoPem(), algorithm = "RSA-OAEP-256" }
            : new { userId = 42, email = "creator@test.local", applicationRole = "Creator", accessToken = "am-token-1", refreshToken = "am-refresh-1" };
        var vJson = JsonSerializer.Serialize(new { success = true, data = vData });
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(vJson, Encoding.UTF8, "application/json") });
    }
}

/// <summary>Phase 3 sign-in: REQ-FN-086, REQ-FN-087, REQ-UI-099.</summary>
public class DesktopSignInTests
{
    private static readonly SignInSettings Settings = new("https://appmanager.test.invalid/", 7, "ak_test_desktop_key");

    private static async Task<(DesktopEngine Engine, FakeAppManager AppManager)> StartAsync()
    {
        var vFake = new FakeAppManager();
        var vEngine = await DesktopEngine.StartAsync(aSignIn: Settings, aConfigure: aServices =>
            aServices.AddHttpClient<IAppManagerClient, AppManagerClient>().ConfigurePrimaryHttpMessageHandler(() => vFake));
        return (vEngine, vFake);
    }

    private static void AssertIdAndKeyOnly(HttpRequestMessage aRequest)
    {
        Assert.Equal("7", Assert.Single(aRequest.Headers.GetValues(AppManagerHeaders.ApplicationId)));
        Assert.Equal("ak_test_desktop_key", Assert.Single(aRequest.Headers.GetValues(AppManagerHeaders.ApiKey)));
        Assert.False(aRequest.Headers.Contains("X-Api-Secret"), "no API secret header is sent");
    }

    /// <summary>REQ-FN-086: login carries the application id and the API key and no secret.</summary>
    [Fact(DisplayName = "REQ-FN-086 LoginSendsAppIdAndKeyAndNoSecret")]
    public async Task LoginSendsAppIdAndKeyAndNoSecret()
    {
        var (vEngine, vFake) = await StartAsync();
        await using var _ = vEngine;
        using var vClient = vEngine.Client();

        var vResponse = await vClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Email = "creator@test.local", Password = "pass-word-1" });

        Assert.Equal(HttpStatusCode.OK, vResponse.StatusCode);
        var vLogin = vFake.Requests.Single(aRequest => aRequest.RequestUri!.AbsolutePath.EndsWith("AuthSvc/login", StringComparison.Ordinal));
        AssertIdAndKeyOnly(vLogin);
        Assert.Equal(1, vEngine.Host.Sessions.Count);
    }

    /// <summary>REQ-FN-087: register carries the application id and the API key and no secret.</summary>
    [Fact(DisplayName = "REQ-FN-087 RegisterSendsAppIdAndKeyAndNoSecret")]
    public async Task RegisterSendsAppIdAndKeyAndNoSecret()
    {
        var (vEngine, vFake) = await StartAsync();
        await using var _ = vEngine;
        using var vClient = vEngine.Client();

        var vResponse = await vClient.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest { Name = "New Creator", Email = "new@test.local", Password = "pass-word-1" });

        Assert.Equal(HttpStatusCode.OK, vResponse.StatusCode);
        AssertIdAndKeyOnly(vFake.Requests.Single(aRequest => aRequest.RequestUri!.AbsolutePath.EndsWith("AuthSvc/register", StringComparison.Ordinal)));
    }

    /// <summary>REQ-UI-099: an unreachable AppManager answers with the plain sign-in-service message.</summary>
    [Fact(DisplayName = "REQ-UI-099 UnreachableServiceGivesPlainMessage")]
    public async Task UnreachableServiceGivesPlainMessage()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aSignIn: new SignInSettings("http://127.0.0.1:9/", 7, "ak_test"));
        using var vClient = vEngine.Client();

        var vResponse = await vClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Email = "creator@test.local", Password = "pass-word-1" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, vResponse.StatusCode);
        var vBody = await vResponse.Content.ReadAsStringAsync();
        Assert.Contains("SIGN_IN_UNREACHABLE", vBody);
        Assert.Contains("cannot be reached", vBody);
    }
}

/// <summary>Phase 3: REQ-UI-098, REQ-NFR-019 — the build's sign-in settings.</summary>
public class SignInSettingsTests
{
    /// <summary>REQ-UI-098: a build without id or key says so and refuses sign-in before calling AppManager.</summary>
    [Fact(DisplayName = "REQ-UI-098 MissingIdOrKeyIsReported")]
    public async Task MissingIdOrKeyIsReported()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aSignIn: SignInSettings.None);
        using var vClient = vEngine.Client();

        var vSettings = await vClient.GetFromJsonAsync<JsonElement>(DesktopSessionEndpoints.SignInSettingsRoute);
        Assert.False(vSettings.GetProperty("configured").GetBoolean());
        Assert.Equal(DesktopSessionEndpoints.MissingSettingsMessage, vSettings.GetProperty("message").GetString());

        var vLogin = await vClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Email = "a@b.c", Password = "x" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, vLogin.StatusCode);
        Assert.Contains("SIGN_IN_NOT_CONFIGURED", await vLogin.Content.ReadAsStringAsync());

        Assert.False(new SignInSettings("https://a/", 7, null).IsConfigured);
        Assert.False(new SignInSettings("https://a/", null, "k").IsConfigured);
        Assert.True(new SignInSettings("https://a/", 7, "k").IsConfigured);
    }

    /// <summary>REQ-NFR-019: the id and key come from a git-ignored file embedded into the build; the committed template is empty.</summary>
    [Fact(DisplayName = "REQ-NFR-019 BuildFileIsIgnoredAndEmbedded")]
    public void BuildFileIsIgnoredAndEmbedded()
    {
        var vRoot = TrStudio.Tests.Unit.Venice.RepoPaths.Root;
        var vIgnore = File.ReadAllText(Path.Combine(vRoot, ".gitignore"));
        Assert.Contains("src/TrStudio.Desktop/appmanager.local.json", vIgnore);

        var vProject = File.ReadAllText(Path.Combine(vRoot, "src", "TrStudio.Desktop", "TrStudio.Desktop.csproj"));
        Assert.Contains("LogicalName=\"TrStudio.Desktop.appmanager.local.json\"", vProject);
        Assert.Contains("LogicalName=\"TrStudio.Desktop.appmanager.template.json\"", vProject);

        using var vTemplate = JsonDocument.Parse(File.ReadAllText(Path.Combine(vRoot, "src", "TrStudio.Desktop", "appmanager.template.json")));
        var vSection = vTemplate.RootElement.GetProperty("AppManager");
        Assert.Equal(string.Empty, vSection.GetProperty("ApiKey").GetString());
        Assert.Equal(0, vSection.GetProperty("ApplicationId").GetInt32());
        Assert.False(vSection.TryGetProperty("ApiSecret", out _), "no API secret exists");
        Assert.False(vSection.TryGetProperty("JwtSigningKey", out _), "no signing key exists");
    }
}

/// <summary>Phase 3: REQ-FN-088 and REQ-NFR-016 — the in-process handler and the session.</summary>
public class InProcessEngineHandlerTests
{
    /// <summary>REQ-NFR-016: the health route answers through the in-process handler, with no socket or server.</summary>
    [Fact(DisplayName = "REQ-NFR-016 HealthAnswersWithoutASocket")]
    public async Task HealthAnswersWithoutASocket()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vClient = vEngine.Client();

        var vResponse = await vClient.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, vResponse.StatusCode);
        Assert.Equal("trstudio.local", vResponse.RequestMessage!.RequestUri!.Host);
        Assert.IsType<InProcessEngineHandler>(typeof(HttpMessageInvoker)
            .GetField("_handler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(vClient));
    }

    /// <summary>REQ-FN-088: signed out, a protected route answers 401; signed in, 200; after sign-out, 401 again.</summary>
    [Fact(DisplayName = "REQ-FN-088 SignedOutCallAnswersUnauthorized")]
    public async Task SignedOutCallAnswersUnauthorized()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vAnonymous = vEngine.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await vAnonymous.GetAsync("/api/v1/jobs")).StatusCode);

        var vToken = vEngine.SignIn(UserRole.Creator);
        using var vSignedIn = vEngine.Client(vToken);
        Assert.Equal(HttpStatusCode.OK, (await vSignedIn.GetAsync("/api/v1/jobs")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await vSignedIn.PostAsync(DesktopSessionEndpoints.SignOutRoute, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await vSignedIn.GetAsync("/api/v1/jobs")).StatusCode);
    }

    /// <summary>REQ-FN-088: one user's job is not found for another user (the owner filter in process).</summary>
    [Fact(DisplayName = "REQ-FN-088 OtherUsersAssetIsNotFound")]
    public async Task OtherUsersAssetIsNotFound()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vUserA = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-a"));
        using var vUserB = vEngine.Client(vEngine.SignIn(UserRole.Creator, "user-b"));

        var vJobId = await DesktopEngine.SubmitStubJobAsync(vUserA);
        Assert.Equal("Succeeded", await vEngine.WaitForEndAsync(vJobId));

        Assert.Equal(HttpStatusCode.OK, (await vUserA.GetAsync($"/api/v1/jobs/{vJobId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await vUserB.GetAsync($"/api/v1/jobs/{vJobId}")).StatusCode);
    }

    /// <summary>Admin routes keep the Owner or Operator check in process.</summary>
    [Fact(DisplayName = "REQ-FN-088 AdminRouteRefusesACreator")]
    public async Task AdminRouteRefusesACreator()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vCreator = vEngine.Client(vEngine.SignIn(UserRole.Creator));
        using var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Owner, "owner-1"));

        Assert.Equal(HttpStatusCode.Forbidden, (await vCreator.GetAsync("/api/v1/admin/providers/keys")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await vOwner.GetAsync("/api/v1/admin/providers/keys")).StatusCode);
    }
}

/// <summary>Phase 3 keys: REQ-FN-096, REQ-FN-097, REQ-FN-098.</summary>
public class ProviderSettingsTests
{
    /// <summary>REQ-FN-096: a RunPod key and endpoint id saved on Registry and providers make RunPod configured, key masked.</summary>
    [Fact(DisplayName = "REQ-FN-096 SavedRunPodKeyIsConfiguredAndMasked")]
    public async Task SavedRunPodKeyIsConfiguredAndMasked()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Owner, "owner-1"));

        var vResponse = await vOwner.PutAsJsonAsync("/api/v1/admin/providers/keys",
            new ProviderKeyUpsertRequest { ProviderName = "RunPod", ApiKey = "rp-secret-value-7c21", IsEnabled = true, Account = "endpoint-abc" });
        var vBody = await vResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, vResponse.StatusCode);
        Assert.Contains("****7c21", vBody);
        Assert.DoesNotContain("rp-secret-value", vBody);
        Assert.Contains("endpoint-abc", vBody);
        var vOptions = vEngine.Host.Services.GetRequiredService<RunPodOptions>();
        Assert.True(vOptions.IsConfigured);
        Assert.Equal("endpoint-abc", vOptions.EndpointId);
    }

    /// <summary>REQ-FN-098: a HeyGen key saved is in use for the next job without a restart.</summary>
    [Fact(DisplayName = "REQ-FN-098 SavedHeyGenKeyIsUsedWithoutRestart")]
    public async Task SavedHeyGenKeyIsUsedWithoutRestart()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        var vAvatar = vEngine.Host.Services.GetRequiredService<AvatarOptions>();
        Assert.False(vAvatar.HeyGenConfigured);
        using var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Owner, "owner-1"));

        var vResponse = await vOwner.PutAsJsonAsync("/api/v1/admin/providers/keys",
            new ProviderKeyUpsertRequest { ProviderName = "HeyGen", ApiKey = "hg-live-key-0001", IsEnabled = true });

        Assert.Equal(HttpStatusCode.OK, vResponse.StatusCode);
        Assert.True(vAvatar.HeyGenConfigured);
        Assert.Equal("hg-live-key-0001", vAvatar.HeyGenApiKey);
    }
}

/// <summary>Phase 3: REQ-FN-097 — keys are ciphertext in the database, unlocked by the operating system's key.</summary>
public class OsKeyProtectionTests
{
    /// <summary>REQ-FN-097: only ciphertext is stored, and the unlock key comes from the protector.</summary>
    [Fact(DisplayName = "REQ-FN-097 DatabaseHoldsOnlyCiphertextAndUnlockKeyComesFromProtector")]
    public async Task DatabaseHoldsOnlyCiphertextAndUnlockKeyComesFromProtector()
    {
        await using var vEngine = await DesktopEngine.StartAsync();
        using var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Owner, "owner-1"));
        var vCallsBefore = vEngine.Protector.Calls;

        var vResponse = await vOwner.PutAsJsonAsync("/api/v1/admin/providers/keys",
            new ProviderKeyUpsertRequest { ProviderName = "ClosedApi", ApiKey = "fal-plain-secret-9999", IsEnabled = true });

        Assert.Equal(HttpStatusCode.OK, vResponse.StatusCode);
        var vStored = (string)vEngine.Scalar("SELECT \"EncryptedKey\" FROM \"ProviderKey\" WHERE \"ProviderName\" = 'ClosedApi'")!;
        Assert.DoesNotContain("fal-plain-secret", vStored);
        Assert.DoesNotContain("fal-plain-secret", Encoding.UTF8.GetString(Convert.FromBase64String(vStored)));
        Assert.True(vEngine.Protector.Calls > vCallsBefore, "the unlock key was asked of the operating system's protector");
    }
}

/// <summary>Phase 3: REQ-RAG-006 — script and caption writing take their model from the registry.</summary>
public class TextModelFromRegistryTests
{
    /// <summary>REQ-RAG-006: the enabled text-chat row (address, model, key) gives the model client.</summary>
    [Fact(DisplayName = "REQ-RAG-006 ScriptUsesTheEnabledTextChatRow")]
    public async Task ScriptUsesTheEnabledTextChatRow()
    {
        var vRegistry = Substitute.For<IRegistryRepository>();
        var vKeys = Substitute.For<IKeyStore>();
        vRegistry.ListAsync(true, Arg.Any<CancellationToken>()).Returns(new List<RegistryModel>
        {
            new() { Id = "image-row", Task = GenerationTaskType.TextToImage, IsEnabled = true },
            new() { Id = "gpt-chat", Task = GenerationTaskType.TextChat, IsEnabled = true, Endpoint = "https://llm.test.invalid/v1", ChatModel = "test-model" }
        });
        vKeys.RetrieveAsync("gpt-chat", Arg.Any<CancellationToken>()).Returns("sk-test-text-model");

        var vSource = new RegistryTextModelSource(vRegistry, vKeys);
        var vResult = await vSource.GetAsync();

        Assert.NotNull(vResult);
        Assert.Equal("gpt-chat", vResult!.Value.ModelId);
        Assert.Equal("test-model", vResult.Value.Provider.ModelName);
    }

    /// <summary>REQ-RAG-006: no text-chat row, or no key, means no model from the registry.</summary>
    [Fact(DisplayName = "REQ-RAG-006 NoTextChatRowGivesNoModel")]
    public async Task NoTextChatRowGivesNoModel()
    {
        var vRegistry = Substitute.For<IRegistryRepository>();
        var vKeys = Substitute.For<IKeyStore>();
        vRegistry.ListAsync(true, Arg.Any<CancellationToken>()).Returns(new List<RegistryModel>
        {
            new() { Id = "gpt-chat", Task = GenerationTaskType.TextChat, IsEnabled = true, Endpoint = "https://llm.test.invalid/v1", ChatModel = "m" }
        });
        vKeys.RetrieveAsync("gpt-chat", Arg.Any<CancellationToken>()).Returns((string?)null);

        Assert.Null(await new RegistryTextModelSource(vRegistry, vKeys).GetAsync());
    }
}
