namespace TrStudio.Engine.Infrastructure.Identity;

/// <summary>
/// Wire shape of an AppManager license summary (LicenseSvc / login <c>activeLicense</c>).
/// </summary>
internal sealed class AppManagerLicenseData
{
    /// <summary>The license id.</summary>
    public long LicenseId { get; set; }

    /// <summary>The license display name (e.g. "Professional").</summary>
    public string? LicenseName { get; set; }

    /// <summary>The license status label (e.g. "Active", "Expired").</summary>
    public string? Status { get; set; }

    /// <summary>UTC expiry date of the license.</summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>Days remaining before expiry.</summary>
    public int? DaysRemaining { get; set; }
}
