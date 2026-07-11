using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrStudio.Engine.Domain.Identity;
using TrStudio.Models.Contracts;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Infrastructure.Identity;

/// <summary>
/// Typed HTTP client for the AppManager child-app API (guide v1.4): RSA-encrypted password
/// login/registration, token refresh/validation, license validation (LicenseSvc) and feature
/// gating (FeatureSvc). TrStudio never stores passwords — plaintext values are RSA-encrypted
/// (RSA-OAEP-SHA256) with the server's public key before transmission and then discarded.
/// </summary>
public class AppManagerClient : IAppManagerClient
{
    private const string PublicKeyCacheKey = "AppManager:PublicKey";
    private const string SessionCachePrefix = "AppManager:Session:";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static int HasWarnedMissingCredentials;

    private readonly HttpClient objHttpClient;
    private readonly AppManagerOptions objOptions;
    private readonly IMemoryCache objCache;
    private readonly ILogger<AppManagerClient> objLogger;

    /// <summary>
    /// Initialises the client over the configured <see cref="HttpClient"/> (base address and
    /// <c>X-Api-Key</c>/<c>X-Api-Secret</c> headers are applied by the identity module).
    /// </summary>
    /// <param name="aHttpClient">The typed HTTP client bound to the AppManager base URL.</param>
    /// <param name="aOptions">The AppManager integration options.</param>
    /// <param name="aCache">In-process cache for the public key and per-user session tokens.</param>
    /// <param name="aLogger">Logger for integration diagnostics (never logs credentials).</param>
    public AppManagerClient(
        HttpClient aHttpClient,
        IOptions<AppManagerOptions> aOptions,
        IMemoryCache aCache,
        ILogger<AppManagerClient> aLogger)
    {
        objHttpClient = aHttpClient;
        objOptions = aOptions.Value;
        objCache = aCache;
        objLogger = aLogger;
        WarnOnceWhenUnconfigured();
    }

    /// <inheritdoc />
    public async Task<LoginResponse> LoginAsync(LoginRequest aRequest, CancellationToken aCancellationToken = default)
    {
        var vEncryptedPassword = await EnsureEncryptedAsync(aRequest.Password, aCancellationToken).ConfigureAwait(false);
        var vBody = new Dictionary<string, object?> { ["email"] = aRequest.Email, ["encryptedPassword"] = vEncryptedPassword };
        AddApplicationId(vBody);

        var vEnvelope = await SendAsync<AppManagerAuthData>(HttpMethod.Post, "AuthSvc/login", vBody, null, aCancellationToken).ConfigureAwait(false);
        return ToLoginResponse(vEnvelope.Data!);
    }

    /// <inheritdoc />
    public async Task<LoginResponse> RegisterAsync(RegisterRequest aRequest, CancellationToken aCancellationToken = default)
    {
        if (string.Equals(aRequest.Email, objOptions.SuperAdminEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new AppManagerApiException(400, "SUPERADMIN_RESERVED",
                "The superadmin account pre-exists in AppManager and cannot be registered.");
        }

        var vEncryptedPassword = await EnsureEncryptedAsync(aRequest.Password, aCancellationToken).ConfigureAwait(false);
        var (vFirstName, vLastName) = UserRoleMapper.SplitDisplayName(aRequest.Name);
        var vBody = new Dictionary<string, object?>
        {
            ["email"] = aRequest.Email,
            ["encryptedPassword"] = vEncryptedPassword,
            ["firstName"] = vFirstName,
            ["lastName"] = vLastName,
            ["applicationRoleCode"] = UserRoleMapper.MapAccountTypeToRoleCode(aRequest.AccountType)
        };
        AddApplicationId(vBody);

        var vEnvelope = await SendAsync<AppManagerAuthData>(HttpMethod.Post, "AuthSvc/register", vBody, null, aCancellationToken).ConfigureAwait(false);
        return ToLoginResponse(vEnvelope.Data!);
    }

    /// <inheritdoc />
    public async Task RequestPasswordResetAsync(ForgotPasswordRequest aRequest, CancellationToken aCancellationToken = default)
    {
        var vBody = new Dictionary<string, object?> { ["email"] = aRequest.Email };
        await SendAsync<object>(HttpMethod.Post, "AuthSvc/forgot-password", vBody, null, aCancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> ValidateTokenAsync(string aToken, CancellationToken aCancellationToken = default)
    {
        try
        {
            var vBody = new Dictionary<string, object?> { ["accessToken"] = aToken };
            var vEnvelope = await SendAsync<AppManagerTokenValidationData>(HttpMethod.Post, "AuthSvc/validate", vBody, null, aCancellationToken).ConfigureAwait(false);
            return vEnvelope.Data?.IsValid == true;
        }
        catch (AppManagerApiException vException)
        {
            objLogger.LogWarning("Token validation against AppManager failed ({ErrorCode}); treating token as invalid.", vException.ErrorCode);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetLicenseAsync(string aUserId, CancellationToken aCancellationToken = default)
    {
        var vSessionToken = GetSessionToken(aUserId);
        if (vSessionToken == null)
        {
            objLogger.LogDebug("No cached AppManager session for user {UserId}; cannot fetch licenses.", aUserId);
            return null;
        }

        var vEnvelope = await SendAsync<JsonElement>(HttpMethod.Get, "LicenseSvc", null, vSessionToken, aCancellationToken).ConfigureAwait(false);
        var vData = vEnvelope.Data;
        if (vData.ValueKind == JsonValueKind.Undefined || vData.ValueKind == JsonValueKind.Null
            || (vData.ValueKind == JsonValueKind.Array && vData.GetArrayLength() == 0))
        {
            return null;
        }

        return vData.GetRawText();
    }

    /// <inheritdoc />
    public async Task<bool> IsFeatureEnabledAsync(string aUserId, string aFeatureName, CancellationToken aCancellationToken = default)
    {
        var vSessionToken = GetSessionToken(aUserId);
        if (vSessionToken == null)
        {
            objLogger.LogWarning("No cached AppManager session for user {UserId}; feature {Feature} check fails closed.", aUserId, aFeatureName);
            return false;
        }

        return await IsFeatureEnabledForTokenAsync(vSessionToken, aFeatureName, aCancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> GetPublicKeyAsync(CancellationToken aCancellationToken = default)
    {
        if (objCache.TryGetValue(PublicKeyCacheKey, out string? vCachedKey) && !string.IsNullOrEmpty(vCachedKey))
        {
            return vCachedKey;
        }

        var vEnvelope = await SendAsync<AppManagerPublicKeyData>(HttpMethod.Get, "AuthSvc/public-key", null, null, aCancellationToken).ConfigureAwait(false);
        var vPublicKey = vEnvelope.Data?.PublicKey;
        if (string.IsNullOrWhiteSpace(vPublicKey))
        {
            throw new AppManagerApiException(502, "KEY_FETCH_FAILED", "AppManager did not return an RSA public key.");
        }

        objCache.Set(PublicKeyCacheKey, vPublicKey, TimeSpan.FromHours(12));
        return vPublicKey;
    }

    /// <inheritdoc />
    public async Task<RefreshTokenResponse> RefreshAsync(string aRefreshToken, CancellationToken aCancellationToken = default)
    {
        var vBody = new Dictionary<string, object?> { ["refreshToken"] = aRefreshToken };
        var vEnvelope = await SendAsync<AppManagerRefreshData>(HttpMethod.Post, "AuthSvc/refresh", vBody, null, aCancellationToken).ConfigureAwait(false);
        var vData = vEnvelope.Data!;
        return new RefreshTokenResponse
        {
            Token = vData.AccessToken,
            RefreshToken = vData.RefreshToken,
            ExpiresAt = vData.ExpiresAt ?? DateTime.UtcNow.AddHours(1)
        };
    }

    /// <inheritdoc />
    public async Task<LicenseStatusResponse> GetLicenseStatusAsync(string aAccessToken, CancellationToken aCancellationToken = default)
    {
        try
        {
            var vEnvelope = await SendAsync<AppManagerLicenseValidationData>(HttpMethod.Post, "LicenseSvc/validate", null, aAccessToken, aCancellationToken).ConfigureAwait(false);
            return ToLicenseStatus(vEnvelope.Data);
        }
        catch (AppManagerApiException vException) when (vException.ErrorCode is "LICENSE_NOT_FOUND" or "NOT_FOUND")
        {
            return AbsentLicenseStatus();
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsFeatureEnabledForTokenAsync(string aAccessToken, string aFeatureName, CancellationToken aCancellationToken = default)
    {
        try
        {
            var vEnvelope = await SendAsync<AppManagerFeatureData>(HttpMethod.Get, $"FeatureSvc/{Uri.EscapeDataString(aFeatureName)}", null, aAccessToken, aCancellationToken).ConfigureAwait(false);
            return vEnvelope.Data?.HasAccess == true;
        }
        catch (AppManagerApiException vException)
        {
            objLogger.LogWarning("Feature check {Feature} failed upstream ({ErrorCode}); failing closed.", aFeatureName, vException.ErrorCode);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<UserProfileResponse> GetProfileAsync(string aAccessToken, CancellationToken aCancellationToken = default)
    {
        var vEnvelope = await SendAsync<AppManagerProfileData>(HttpMethod.Get, "UserSvc/profile", null, aAccessToken, aCancellationToken).ConfigureAwait(false);
        var vData = vEnvelope.Data!;
        return new UserProfileResponse
        {
            UserId = vData.UserId.ToString(),
            Email = vData.Email,
            FirstName = vData.FirstName ?? string.Empty,
            LastName = vData.LastName ?? string.Empty,
            Role = UserRoleMapper.MapRole(vData.Email, null, vData.ApplicationRole, objOptions.SuperAdminEmail)
        };
    }

    private static LicenseStatusResponse ToLicenseStatus(AppManagerLicenseValidationData? aData)
    {
        if (aData?.License == null)
        {
            return AbsentLicenseStatus();
        }

        var vIsValid = aData.IsValid;
        return new LicenseStatusResponse
        {
            HasLicense = true,
            IsValid = vIsValid,
            Status = aData.License.Status ?? (vIsValid ? "Active" : "Invalid"),
            LicenseName = aData.License.LicenseName,
            ExpiryDate = aData.License.ExpiryDate,
            DaysRemaining = aData.License.DaysRemaining,
            Message = vIsValid
                ? "License is active."
                : "Your license is expired or inactive. Renew it in AppManager to restore gated tiers."
        };
    }

    private static LicenseStatusResponse AbsentLicenseStatus()
    {
        return new LicenseStatusResponse
        {
            HasLicense = false,
            IsValid = false,
            Status = "None",
            Message = "No license found for this application. Purchase or assign one in AppManager."
        };
    }

    private async Task<string> EnsureEncryptedAsync(string aPassword, CancellationToken aCancellationToken)
    {
        var vPublicKey = await GetPublicKeyAsync(aCancellationToken).ConfigureAwait(false);
        return AppManagerPasswordEncryptor.EnsureEncrypted(aPassword, vPublicKey);
    }

    private LoginResponse ToLoginResponse(AppManagerAuthData aData)
    {
        CacheSessionToken(aData);
        return new LoginResponse
        {
            Token = aData.AccessToken,
            RefreshToken = aData.RefreshToken,
            UserId = aData.UserId.ToString(),
            Role = UserRoleMapper.MapRole(aData.Email, aData.AppManagerRole, aData.ApplicationRole, objOptions.SuperAdminEmail)
        };
    }

    private void CacheSessionToken(AppManagerAuthData aData)
    {
        var vExpiry = aData.TokenExpiresAt ?? DateTime.UtcNow.AddMinutes(55);
        if (vExpiry > DateTime.UtcNow)
        {
            objCache.Set(SessionCachePrefix + aData.UserId, aData.AccessToken, vExpiry);
        }
    }

    private string? GetSessionToken(string aUserId)
    {
        return objCache.TryGetValue(SessionCachePrefix + aUserId, out string? vToken) ? vToken : null;
    }

    private void AddApplicationId(Dictionary<string, object?> aBody)
    {
        // The API key headers resolve the ApplicationId; the explicit body field is a fallback.
        if (objOptions.ApplicationId.HasValue)
        {
            aBody["applicationId"] = objOptions.ApplicationId.Value;
        }
    }

    private async Task<AppManagerEnvelope<T>> SendAsync<T>(HttpMethod aMethod, string aPath, object? aBody, string? aBearerToken, CancellationToken aCancellationToken)
    {
        using var vRequest = new HttpRequestMessage(aMethod, aPath);
        if (aBody != null)
        {
            vRequest.Content = JsonContent.Create(aBody, options: JsonOptions);
        }

        if (!string.IsNullOrEmpty(aBearerToken))
        {
            vRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aBearerToken);
        }

        using var vResponse = await SendRawAsync(vRequest, aCancellationToken).ConfigureAwait(false);
        return await ReadEnvelopeAsync<T>(vResponse, aCancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
    {
        try
        {
            return await objHttpClient.SendAsync(aRequest, aCancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException vException)
        {
            throw new AppManagerApiException(503, "APPMANAGER_UNREACHABLE",
                $"AppManager at {objHttpClient.BaseAddress} is unreachable: {vException.Message}", vException);
        }
        catch (TaskCanceledException vException) when (!aCancellationToken.IsCancellationRequested)
        {
            throw new AppManagerApiException(504, "APPMANAGER_TIMEOUT",
                "AppManager did not respond within the configured timeout.", vException);
        }
    }

    private static async Task<AppManagerEnvelope<T>> ReadEnvelopeAsync<T>(HttpResponseMessage aResponse, CancellationToken aCancellationToken)
    {
        var vContent = await aResponse.Content.ReadAsStringAsync(aCancellationToken).ConfigureAwait(false);
        AppManagerEnvelope<T>? vEnvelope = null;
        try
        {
            vEnvelope = JsonSerializer.Deserialize<AppManagerEnvelope<T>>(vContent, JsonOptions);
        }
        catch (JsonException)
        {
            // Fall through to the UPSTREAM_ERROR below — never leak raw upstream bodies.
        }

        if (vEnvelope == null)
        {
            throw new AppManagerApiException((int)aResponse.StatusCode, "UPSTREAM_ERROR",
                $"AppManager returned an unreadable response (HTTP {(int)aResponse.StatusCode}).");
        }

        if (!vEnvelope.Success)
        {
            ThrowForError(vEnvelope.Error, vEnvelope.Message, (int)aResponse.StatusCode);
        }

        return vEnvelope;
    }

    private static void ThrowForError(string? aErrorCode, string? aMessage, int aStatusCode)
    {
        var vMessage = string.IsNullOrWhiteSpace(aMessage) ? "AppManager request failed." : aMessage;
        if (aErrorCode is "INVALID_CREDENTIALS" or "UNAUTHORIZED")
        {
            throw new UnauthorizedAccessException(vMessage);
        }

        var vStatusCode = aStatusCode is >= 400 and <= 599 ? aStatusCode : 502;
        throw new AppManagerApiException(vStatusCode, aErrorCode ?? "UPSTREAM_ERROR", vMessage);
    }

    private void WarnOnceWhenUnconfigured()
    {
        var vIsUnconfigured = string.IsNullOrWhiteSpace(objOptions.ApiKey) && !objOptions.ApplicationId.HasValue;
        if (vIsUnconfigured && Interlocked.Exchange(ref HasWarnedMissingCredentials, 1) == 0)
        {
            objLogger.LogWarning(
                "AppManager child-app credentials are not configured: set AppManager:ApiKey/AppManager:ApiSecret " +
                "(created in the AppManager admin UI under this application's settings) or AppManager:ApplicationId. " +
                "Login works unscoped, but register/forgot-password/reset will fail with APPLICATION_ID_REQUIRED " +
                "and licenses/roles will not be scoped to TrStudio.");
        }
    }
}
