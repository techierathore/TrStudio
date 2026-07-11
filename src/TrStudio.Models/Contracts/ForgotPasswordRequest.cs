namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for initiating a password reset (<c>POST /api/v1/auth/forgot-password</c>),
/// forwarded to AppManager.
/// </summary>
public class ForgotPasswordRequest
{
    /// <summary>The email address of the account to reset.</summary>
    public string Email { get; set; } = string.Empty;
}
