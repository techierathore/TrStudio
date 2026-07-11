namespace TrStudio.Engine.Infrastructure.Identity;

/// <summary>
/// Wire payload of <c>POST /AuthSvc/login</c> and <c>POST /AuthSvc/register</c>.
/// </summary>
internal sealed class AppManagerAuthData
{
    /// <summary>The AppManager user id.</summary>
    public long UserId { get; set; }

    /// <summary>The user's email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The user's first name.</summary>
    public string? FirstName { get; set; }

    /// <summary>The user's last name.</summary>
    public string? LastName { get; set; }

    /// <summary>The application-scoped role code.</summary>
    public string? ApplicationRole { get; set; }

    /// <summary>The AppManager-level role (e.g. "ApplicationUser", "SuperAdmin").</summary>
    public string? AppManagerRole { get; set; }

    /// <summary>The JWT access token for the session.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>The refresh token for renewing the session.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>UTC expiry of the access token.</summary>
    public DateTime? TokenExpiresAt { get; set; }

    /// <summary>The app-scoped active license, when the user holds one.</summary>
    public AppManagerLicenseData? ActiveLicense { get; set; }
}
