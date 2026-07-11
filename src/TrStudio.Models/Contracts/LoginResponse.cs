using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// Response body for a successful login, carrying the AppManager session and routing hints.
/// </summary>
public class LoginResponse
{
    /// <summary>The JWT bearer token for the user session.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>The refresh token used to renew the session.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>The AppManager user id; TrStudio's ownership reference (never the app DB).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The application role resolved for this user.</summary>
    public UserRole Role { get; set; }

    /// <summary>The path the UI should land on after login (setup gate aware).</summary>
    public string DefaultLandingPath { get; set; } = "/";

    /// <summary>Whether first-run setup has been completed on this instance.</summary>
    public bool SetupComplete { get; set; }
}
