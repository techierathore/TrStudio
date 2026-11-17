using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TrStudio.TestHost;

/// <summary>
/// TEST ONLY — never shipped. A stand-in AppManager answering inside the test host, so the headless
/// checks of "Remember me" (owner decision 6B, REQ-FN-100, REQ-UI-105, REQ-UI-106) can sign in without
/// creating, changing or using any real AppManager account. It answers the calls the real
/// <c>AppManagerClient</c> makes (public key, login, refresh, profile, logout), issues a new refresh
/// token on every refresh as AppManager does (guide §2.3), and can revoke every token it issued to
/// stand in for a server-side revoke. Turned on with <c>/test/sign-in-mode?mode=fake</c>.
/// </summary>
public sealed class FakeAppManager
{
    private static readonly RSA Rsa = RSA.Create(2048);

    private readonly ConcurrentDictionary<string, string> objRefreshTokens = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> objAccessTokens = new(StringComparer.Ordinal);

    /// <summary>Whether the engine's AppManager calls go to this stand-in.</summary>
    public bool Active { get; set; }

    /// <summary>How many logins it answered (a check proves a second open asked for none).</summary>
    public int LoginCount;

    /// <summary>
    /// The licence LicenseSvc/validate answers with (the Account sample data puts one in); while it is
    /// null the stand-in answers "not found", so the app shows "No license" as before.
    /// </summary>
    public FakeLicense? License { get; set; }

    /// <summary>Revokes every refresh token it issued, as a server-side revoke would.</summary>
    public void RevokeAll() => objRefreshTokens.Clear();

    /// <summary>Answers one AppManager call.</summary>
    /// <param name="aRequest">The request.</param>
    /// <returns>The response.</returns>
    public async Task<HttpResponseMessage> AnswerAsync(HttpRequestMessage aRequest)
    {
        var vPath = aRequest.RequestUri!.AbsolutePath.TrimEnd('/');
        var vBody = aRequest.Content is null ? "{}" : await aRequest.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var vJson = JsonDocument.Parse(string.IsNullOrWhiteSpace(vBody) ? "{}" : vBody);
        var vRoot = vJson.RootElement;

        if (vPath.EndsWith("public-key", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { publicKey = Rsa.ExportSubjectPublicKeyInfoPem(), algorithm = "RSA-OAEP-256" });
        }

        if (vPath.EndsWith("AuthSvc/login", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref LoginCount);
            var vEmail = vRoot.TryGetProperty("email", out var vEmailElement) ? vEmailElement.GetString() ?? "owner@test.local" : "owner@test.local";
            return Ok(Issue(vEmail));
        }

        if (vPath.EndsWith("AuthSvc/refresh", StringComparison.OrdinalIgnoreCase))
        {
            var vToken = vRoot.TryGetProperty("refreshToken", out var vTokenElement) ? vTokenElement.GetString() ?? string.Empty : string.Empty;
            if (!objRefreshTokens.TryRemove(vToken, out var vOwner))
            {
                return Refuse("REVOKED_REFRESH_TOKEN", "Refresh token has been revoked");
            }

            var vIssued = Issue(vOwner);
            return Ok(new { accessToken = vIssued.accessToken, refreshToken = vIssued.refreshToken, expiresAt = DateTime.UtcNow.AddHours(1) });
        }

        if (vPath.EndsWith("UserSvc/profile", StringComparison.OrdinalIgnoreCase))
        {
            var vBearer = aRequest.Headers.Authorization?.Parameter ?? string.Empty;
            return objAccessTokens.TryGetValue(vBearer, out var vEmail)
                ? Ok(new { userId = 4242, email = vEmail, firstName = "Test", lastName = "Owner", applicationRole = "Admin" })
                : Refuse("UNAUTHORIZED", "Invalid token");
        }

        if (vPath.EndsWith("LicenseSvc/validate", StringComparison.OrdinalIgnoreCase) && License is { } vLicense)
        {
            var vDays = (int)Math.Ceiling((vLicense.ExpiryUtc - DateTime.UtcNow).TotalDays);
            return Ok(new { isValid = true, license = new { licenseId = 1, licenseName = vLicense.Name, status = vLicense.Tier, expiryDate = vLicense.ExpiryUtc, daysRemaining = vDays } });
        }

        if (vPath.EndsWith("AuthSvc/logout", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { });
        }

        return Refuse("NOT_FOUND", "Not answered by the test stand-in");
    }

    private Issued Issue(string aEmail)
    {
        var vAccess = "fake-access-" + Guid.NewGuid().ToString("N");
        var vRefresh = "fake-refresh-" + Guid.NewGuid().ToString("N");
        objAccessTokens[vAccess] = aEmail;
        objRefreshTokens[vRefresh] = aEmail;
        return new Issued(4242, aEmail, "Admin", "ApplicationUser", vAccess, vRefresh, DateTime.UtcNow.AddHours(1));
    }

    private static HttpResponseMessage Ok(object aData) => Json(HttpStatusCode.OK, new { success = true, data = aData, message = "ok" });

    private static HttpResponseMessage Refuse(string aCode, string aMessage)
        => Json(HttpStatusCode.Unauthorized, new { success = false, error = aCode, message = aMessage });

    private static HttpResponseMessage Json(HttpStatusCode aStatus, object aBody)
        => new(aStatus) { Content = new StringContent(JsonSerializer.Serialize(aBody), Encoding.UTF8, "application/json") };

    // Property names follow AppManager's login data (camelCase on the wire).
#pragma warning disable IDE1006
    private sealed record Issued(int userId, string email, string applicationRole, string appManagerRole, string accessToken, string refreshToken, DateTime tokenExpiresAt);
#pragma warning restore IDE1006
}

/// <summary>TEST ONLY: a licence the stand-in AppManager's LicenseSvc reports.</summary>
/// <param name="Name">The licence name (e.g. "Creator Pro").</param>
/// <param name="Tier">The licence status / tier badge (e.g. "Pro").</param>
/// <param name="ExpiryUtc">When it renews / expires.</param>
public sealed record FakeLicense(string Name, string Tier, DateTime ExpiryUtc);

/// <summary>TEST ONLY: sends AppManager calls to the stand-in while it is active, else to the network.</summary>
public sealed class FakeAppManagerSwitch : DelegatingHandler
{
    private readonly FakeAppManager objFake;

    /// <summary>Creates the switch.</summary>
    /// <param name="aFake">The stand-in.</param>
    public FakeAppManagerSwitch(FakeAppManager aFake)
        : base(new SocketsHttpHandler())
        => objFake = aFake;

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
        => objFake.Active ? objFake.AnswerAsync(aRequest) : base.SendAsync(aRequest, aCancellationToken);
}
