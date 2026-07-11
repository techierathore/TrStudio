namespace TrStudio.Engine.Infrastructure.Identity;

/// <summary>
/// Wire payload of <c>GET /FeatureSvc/{aFeatureCode}</c>.
/// </summary>
internal sealed class AppManagerFeatureData
{
    /// <summary>The feature code that was checked.</summary>
    public string FeatureCode { get; set; } = string.Empty;

    /// <summary>Whether the caller has access to the feature.</summary>
    public bool HasAccess { get; set; }

    /// <summary>Why access is denied, when it is (e.g. "Feature not included in your license").</summary>
    public string? Reason { get; set; }

    /// <summary>The license tier required to unlock the feature, when reported.</summary>
    public string? RequiredLicense { get; set; }
}
