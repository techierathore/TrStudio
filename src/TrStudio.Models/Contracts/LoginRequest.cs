namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for user login (<c>POST /api/v1/auth/login</c>), forwarded to AppManager.
/// </summary>
public class LoginRequest
{
    /// <summary>The user's email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The user's password, RSA-encrypted client-side per the AppManager handshake — never plaintext.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// "Remember me" (owner decision 6B, BRD-153): when true, the desktop app keeps the AppManager
    /// refresh token on this computer, protected for this user, and signs in with it on the next open
    /// for up to 30 days. Never sent to AppManager.
    /// </summary>
    public bool RememberMe { get; set; }
}
