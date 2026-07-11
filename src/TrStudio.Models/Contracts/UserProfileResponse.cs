using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// Response body of the current-user endpoint (<c>GET /api/v1/auth/me</c>): the caller's
/// AppManager profile, resolved application role and routing hints.
/// </summary>
public class UserProfileResponse
{
    /// <summary>The AppManager user id; TrStudio's ownership reference.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The user's email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The user's first name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>The user's last name.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>The application role resolved for this user.</summary>
    public UserRole Role { get; set; }

    /// <summary>The path the UI should land on for this user (setup gate aware).</summary>
    public string DefaultLandingPath { get; set; } = "/";

    /// <summary>Whether first-run setup has been completed on this instance.</summary>
    public bool SetupComplete { get; set; }
}
