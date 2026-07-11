namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for user registration (<c>POST /api/v1/auth/register</c>), forwarded to AppManager.
/// </summary>
public class RegisterRequest
{
    /// <summary>The user's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The user's email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The user's password, RSA-encrypted client-side per the AppManager handshake.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>The requested account type label (AppManager licensing plan).</summary>
    public string AccountType { get; set; } = string.Empty;
}
