namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for renewing a session (<c>POST /api/v1/auth/refresh</c>), forwarded to AppManager.
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>The refresh token issued by AppManager at login.</summary>
    public string RefreshToken { get; set; } = string.Empty;
}
