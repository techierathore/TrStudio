namespace TrStudio.Engine.Infrastructure.Identity;

/// <summary>
/// Wire payload of <c>POST /LicenseSvc/validate</c>.
/// </summary>
internal sealed class AppManagerLicenseValidationData
{
    /// <summary>Whether the user holds a valid license for this application.</summary>
    public bool IsValid { get; set; }

    /// <summary>The validated license summary, when one exists.</summary>
    public AppManagerLicenseData? License { get; set; }
}
