namespace TrStudio.Models.Entities;

/// <summary>
/// The per-deployment safety policy row (SAFE-06 / BRD-65). Stores only the toggleable settings —
/// principally the owner-only Unrestricted (NSFW) mode flag (F-NSFW / BRD-79). The non-disableable
/// floors (SAFE-01..06) are NEVER represented here, so no policy edit can weaken them.
/// </summary>
public class SafetyPolicy
{
    /// <summary>The single-row policy key (always <c>default</c> for this deployment).</summary>
    public string PolicyKey { get; set; } = "default";

    /// <summary>
    /// Whether the owner has enabled Unrestricted (NSFW) mode. This is only ever EFFECTIVE in
    /// personal/single-user mode; it is forced off in multi-user/SaaS regardless of this flag.
    /// </summary>
    public bool NsfwModeEnabled { get; set; }

    /// <summary>UTC timestamp when the policy was last updated.</summary>
    public DateTime UpdatedUtc { get; set; }
}
