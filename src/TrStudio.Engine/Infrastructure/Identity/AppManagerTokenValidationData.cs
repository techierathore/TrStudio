namespace TrStudio.Engine.Infrastructure.Identity;

/// <summary>
/// Wire payload of <c>POST /AuthSvc/validate</c>.
/// </summary>
internal sealed class AppManagerTokenValidationData
{
    /// <summary>Whether the access token is valid and current for this application.</summary>
    public bool IsValid { get; set; }

    /// <summary>The AppManager user id the token belongs to.</summary>
    public long? UserId { get; set; }

    /// <summary>The email of the token holder.</summary>
    public string? Email { get; set; }

    /// <summary>The reason the token was judged invalid (e.g. an APP_ID_MISMATCH note).</summary>
    public string? InvalidReason { get; set; }
}
