namespace TrStudio.Models.Contracts;

/// <summary>
/// Response body for a successful token refresh (<c>POST /api/v1/auth/refresh</c>).
/// </summary>
public class RefreshTokenResponse
{
    /// <summary>The renewed JWT bearer token for the user session.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>The rotated refresh token to use for the next renewal.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>UTC timestamp at which the renewed access token expires.</summary>
    public DateTime ExpiresAt { get; set; }
}
