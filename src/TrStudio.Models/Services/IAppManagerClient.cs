using TrStudio.Models.Contracts;

namespace TrStudio.Models.Services;

/// <summary>
/// Client for the external AppManager identity/licensing service (Architecture §7, ADR-007).
/// AppManager provides users, JWT sessions, licences and feature gating — it is never the
/// application database; TrStudio stores only the AppManager user id for ownership.
/// </summary>
public interface IAppManagerClient
{
    /// <summary>
    /// Authenticates a user against AppManager.
    /// </summary>
    /// <param name="aRequest">Email and RSA-encrypted password.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The session tokens, user id, role and routing hints.</returns>
    /// <exception cref="System.UnauthorizedAccessException">The credentials were rejected.</exception>
    Task<LoginResponse> LoginAsync(LoginRequest aRequest, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Registers a new user with AppManager.
    /// </summary>
    /// <param name="aRequest">Name, email, RSA-encrypted password and account type.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The session for the newly created user.</returns>
    Task<LoginResponse> RegisterAsync(RegisterRequest aRequest, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Initiates a password reset flow for an email address via AppManager.
    /// </summary>
    /// <param name="aRequest">The account email address.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the reset request is accepted.</returns>
    Task RequestPasswordResetAsync(ForgotPasswordRequest aRequest, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Validates a user session token with AppManager.
    /// </summary>
    /// <param name="aToken">The JWT bearer token to validate.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> when the token is valid and current; otherwise <c>false</c>.</returns>
    Task<bool> ValidateTokenAsync(string aToken, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Retrieves the licence/subscription descriptor for a user.
    /// </summary>
    /// <param name="aUserId">The AppManager user id.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The licence descriptor JSON, or null when the user has no licence.</returns>
    Task<string?> GetLicenseAsync(string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Checks whether a named feature is enabled for a user (tier gating, Architecture §7).
    /// </summary>
    /// <param name="aUserId">The AppManager user id.</param>
    /// <param name="aFeatureName">The feature flag name (e.g. "PremiumTier").</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> when the feature is enabled for the user; otherwise <c>false</c>.</returns>
    Task<bool> IsFeatureEnabledAsync(string aUserId, string aFeatureName, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Fetches (and caches) the AppManager RSA public key used for client-side password
    /// encryption (guide §2.4, RSA-OAEP-SHA256).
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The PEM-encoded RSA public key.</returns>
    Task<string> GetPublicKeyAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Renews a session using an AppManager refresh token (guide §2.3).
    /// </summary>
    /// <param name="aRefreshToken">The refresh token issued at login or a previous refresh.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The renewed access and rotated refresh tokens.</returns>
    Task<RefreshTokenResponse> RefreshAsync(string aRefreshToken, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Validates the caller's license for this application via AppManager LicenseSvc (BRD-58).
    /// </summary>
    /// <param name="aAccessToken">The caller's AppManager access token.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The license status; an expired or absent license is reported, never thrown.</returns>
    Task<LicenseStatusResponse> GetLicenseStatusAsync(string aAccessToken, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Checks whether a named feature is enabled for the holder of an access token via
    /// AppManager FeatureSvc (request-path variant of <see cref="IsFeatureEnabledAsync"/>).
    /// </summary>
    /// <param name="aAccessToken">The caller's AppManager access token.</param>
    /// <param name="aFeatureName">The AppManager feature code (e.g. "TIER_PREMIUM").</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> when the feature is enabled; <c>false</c> when disabled or undeterminable (fail closed).</returns>
    Task<bool> IsFeatureEnabledForTokenAsync(string aAccessToken, string aFeatureName, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Retrieves the profile of the access-token holder from AppManager UserSvc, with the
    /// application role resolved to a <see cref="TrStudio.Models.Enums.UserRole"/>.
    /// </summary>
    /// <param name="aAccessToken">The caller's AppManager access token.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The user profile; routing fields are filled by the API layer.</returns>
    Task<UserProfileResponse> GetProfileAsync(string aAccessToken, CancellationToken aCancellationToken = default);
}
