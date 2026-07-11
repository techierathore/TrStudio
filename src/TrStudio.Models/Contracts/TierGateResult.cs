using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// Verdict of the server-side tier gate (BRD-59): whether the user may run a job on the
/// requested execution tier, with an upgrade prompt when the tier is license-gated.
/// The job-submit path returns this shape to the UI when a tier is refused.
/// </summary>
public class TierGateResult
{
    /// <summary>Whether the user is allowed to use the requested tier.</summary>
    public bool IsAllowed { get; set; }

    /// <summary>The execution tier that was checked.</summary>
    public GenerationTier Tier { get; set; }

    /// <summary>Machine-readable denial reason code (e.g. "TIER_GATED"), null when allowed.</summary>
    public string? DeniedReason { get; set; }

    /// <summary>The AppManager feature code required to unlock the tier, null when allowed.</summary>
    public string? RequiredFeatureCode { get; set; }

    /// <summary>Human-readable upgrade prompt shown to the user when the tier is refused.</summary>
    public string? UpgradeMessage { get; set; }
}
