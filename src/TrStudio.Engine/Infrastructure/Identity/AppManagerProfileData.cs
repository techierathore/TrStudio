namespace TrStudio.Engine.Infrastructure.Identity;

/// <summary>
/// Wire payload of <c>GET /UserSvc/profile</c>.
/// </summary>
internal sealed class AppManagerProfileData
{
    /// <summary>The AppManager user id.</summary>
    public long UserId { get; set; }

    /// <summary>The user's email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The user's first name.</summary>
    public string? FirstName { get; set; }

    /// <summary>The user's last name.</summary>
    public string? LastName { get; set; }

    /// <summary>The application-scoped role code for the calling app.</summary>
    public string? ApplicationRole { get; set; }
}
