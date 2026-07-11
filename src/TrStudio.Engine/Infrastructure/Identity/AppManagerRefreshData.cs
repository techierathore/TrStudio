namespace TrStudio.Engine.Infrastructure.Identity;

/// <summary>
/// Wire payload of <c>POST /AuthSvc/refresh</c>.
/// </summary>
internal sealed class AppManagerRefreshData
{
    /// <summary>The renewed JWT access token.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>The rotated refresh token.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>UTC expiry of the renewed access token.</summary>
    public DateTime? ExpiresAt { get; set; }
}
