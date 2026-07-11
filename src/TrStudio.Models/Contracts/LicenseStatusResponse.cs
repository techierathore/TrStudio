namespace TrStudio.Models.Contracts;

/// <summary>
/// Response body of the license status endpoint (<c>GET /api/v1/license</c>): the caller's
/// AppManager license validity for this application (BRD-58). An expired or absent license
/// is reported here and gates tier access accordingly.
/// </summary>
public class LicenseStatusResponse
{
    /// <summary>Whether AppManager returned any license for this user and application.</summary>
    public bool HasLicense { get; set; }

    /// <summary>Whether the license is currently valid (active and unexpired).</summary>
    public bool IsValid { get; set; }

    /// <summary>The license status label reported by AppManager (e.g. "Active", "Expired").</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>The license display name (e.g. "Professional"), when one exists.</summary>
    public string? LicenseName { get; set; }

    /// <summary>UTC expiry date of the license, when one exists.</summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>Days remaining before expiry, when AppManager reports it.</summary>
    public int? DaysRemaining { get; set; }

    /// <summary>Human-readable status message (e.g. an upgrade/renewal prompt when invalid).</summary>
    public string Message { get; set; } = string.Empty;
}
