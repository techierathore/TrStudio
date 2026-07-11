using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrStudio.Engine.Infrastructure.Identity;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;

namespace TrStudio.Tests.Unit.Identity;

/// <summary>
/// Tests for <see cref="AppManagerClient"/>: the RSA login handshake, role mapping and clean
/// surfacing of upstream AppManager errors (REQ-FN-012/015).
/// </summary>
public class AppManagerClientTests
{
    private readonly FakeAppManagerHttpHandler objHandler = new();
    private readonly RSA objRsa = RSA.Create(2048);

    /// <summary>
    /// Login fetches the public key, RSA-encrypts the password (decryptable with the matching
    /// private key, OAEP-SHA256), never sends plaintext, and maps the response to a session.
    /// </summary>
    [Fact]
    public async Task LoginEncryptsPasswordAndParsesSession()
    {
        ArrangePublicKey();
        objHandler.Respond("/AuthSvc/login", HttpStatusCode.OK, """
            {"success":true,"data":{"userId":7,"email":"user@example.com","applicationRole":"Creator",
             "appManagerRole":"ApplicationUser","accessToken":"jwt-abc","refreshToken":"rt-xyz",
             "tokenExpiresAt":"2099-01-01T00:00:00Z"},"message":"Login successful"}
            """);
        var vClient = CreateClient();

        var vSession = await vClient.LoginAsync(new LoginRequest { Email = "user@example.com", Password = "MyPass@123" });

        Assert.Equal("jwt-abc", vSession.Token);
        Assert.Equal("rt-xyz", vSession.RefreshToken);
        Assert.Equal("7", vSession.UserId);
        Assert.Equal(UserRole.Creator, vSession.Role);
        AssertPasswordWasEncrypted("MyPass@123");
    }

    /// <summary>
    /// The superadmin bootstrap: admin@appmanager.local logging in maps to the Owner role.
    /// </summary>
    [Fact]
    public async Task SuperAdminLoginMapsToOwner()
    {
        ArrangePublicKey();
        objHandler.Respond("/AuthSvc/login", HttpStatusCode.OK, """
            {"success":true,"data":{"userId":1,"email":"admin@appmanager.local","applicationRole":"User",
             "appManagerRole":"SuperAdmin","accessToken":"jwt-sa","refreshToken":"rt-sa"},"message":"ok"}
            """);
        var vClient = CreateClient();

        var vSession = await vClient.LoginAsync(new LoginRequest { Email = "admin@appmanager.local", Password = "Secret@123" });

        Assert.Equal(UserRole.Owner, vSession.Role);
    }

    /// <summary>
    /// Wrong credentials surface as UnauthorizedAccessException (mapped to a clean 401 by the
    /// endpoint), never a raw upstream failure.
    /// </summary>
    [Fact]
    public async Task LoginRejectsBadCredentials()
    {
        ArrangePublicKey();
        objHandler.Respond("/AuthSvc/login", HttpStatusCode.Unauthorized,
            """{"success":false,"error":"INVALID_CREDENTIALS","message":"Invalid email or password","statusCode":401}""");
        var vClient = CreateClient();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => vClient.LoginAsync(new LoginRequest { Email = "user@example.com", Password = "Wrong@123" }));
    }

    /// <summary>
    /// A duplicate registration email surfaces AppManager's EMAIL_EXISTS error code and 409
    /// status cleanly through AppManagerApiException.
    /// </summary>
    [Fact]
    public async Task RegisterSurfacesDuplicateEmail()
    {
        ArrangePublicKey();
        objHandler.Respond("/AuthSvc/register", HttpStatusCode.Conflict,
            """{"success":false,"error":"EMAIL_EXISTS","message":"Email already registered","statusCode":409}""");
        var vClient = CreateClient();

        var vException = await Assert.ThrowsAsync<AppManagerApiException>(
            () => vClient.RegisterAsync(new RegisterRequest { Name = "Jane Doe", Email = "dup@example.com", Password = "Pass@1234", AccountType = "Creator" }));

        Assert.Equal(409, vException.StatusCode);
        Assert.Equal("EMAIL_EXISTS", vException.ErrorCode);
    }

    /// <summary>
    /// Registering the reserved superadmin email is refused locally — the account pre-exists in
    /// AppManager and must never be created or overwritten.
    /// </summary>
    [Fact]
    public async Task RegisterRefusesSuperAdminEmail()
    {
        var vClient = CreateClient();

        var vException = await Assert.ThrowsAsync<AppManagerApiException>(
            () => vClient.RegisterAsync(new RegisterRequest { Name = "Fake Admin", Email = "admin@appmanager.local", Password = "Pass@1234" }));

        Assert.Equal("SUPERADMIN_RESERVED", vException.ErrorCode);
    }

    /// <summary>
    /// An unreachable AppManager host is wrapped as APPMANAGER_UNREACHABLE (503), keeping the
    /// endpoint response clean instead of a 500 stack trace.
    /// </summary>
    [Fact]
    public async Task UnreachableHostWrapsAsUpstreamError()
    {
        using var vHttpClient = new HttpClient(new ThrowingHttpHandler())
        {
            BaseAddress = new Uri("https://localhost:59999/")
        };
        var vClient = new AppManagerClient(
            vHttpClient,
            Options.Create(new AppManagerOptions()),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<AppManagerClient>.Instance);

        var vException = await Assert.ThrowsAsync<AppManagerApiException>(() => vClient.GetPublicKeyAsync());

        Assert.Equal(503, vException.StatusCode);
        Assert.Equal("APPMANAGER_UNREACHABLE", vException.ErrorCode);
    }

    private void ArrangePublicKey()
    {
        var vPublicKeyPem = objRsa.ExportSubjectPublicKeyInfoPem();
        var vPayload = JsonSerializer.Serialize(new
        {
            success = true,
            data = new { publicKey = vPublicKeyPem, algorithm = "RSA-OAEP-256" },
            message = "ok"
        });
        objHandler.Respond("/AuthSvc/public-key", HttpStatusCode.OK, vPayload);
    }

    private void AssertPasswordWasEncrypted(string aExpectedPassword)
    {
        var vLoginRequest = objHandler.Requests.Single(aRequest => aRequest.Path.EndsWith("/AuthSvc/login", StringComparison.OrdinalIgnoreCase));
        using var vDocument = JsonDocument.Parse(vLoginRequest.Body!);
        Assert.False(vDocument.RootElement.TryGetProperty("password", out _));
        var vCiphertext = vDocument.RootElement.GetProperty("encryptedPassword").GetString()!;
        Assert.NotEqual(aExpectedPassword, vCiphertext);
        var vDecrypted = objRsa.Decrypt(Convert.FromBase64String(vCiphertext), RSAEncryptionPadding.OaepSHA256);
        Assert.Equal(aExpectedPassword, Encoding.UTF8.GetString(vDecrypted));
    }

    private AppManagerClient CreateClient()
    {
        var vHttpClient = new HttpClient(objHandler) { BaseAddress = new Uri("https://localhost:5101/") };
        return new AppManagerClient(
            vHttpClient,
            Options.Create(new AppManagerOptions()),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<AppManagerClient>.Instance);
    }
}
