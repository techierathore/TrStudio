using Microsoft.Extensions.Configuration;

namespace TrStudio.Engine.Domain.Avatar;

/// <summary>
/// Configuration for the avatar pipeline (section <c>Avatar</c>, FN-037..040). No local lip-sync
/// model and no HeyGen key are configured by default, so every tier degrades gracefully with an
/// actionable message (REQ-FN-005 style). <see cref="SyntheticFallback"/> (dev-only, default false)
/// additionally emits a deterministic placeholder video asset so the chain can be exercised end to
/// end without a real model.
/// </summary>
public class AvatarOptions
{
    /// <summary>The HeyGen API key backing the Premium tier; EMPTY by default → "HeyGen not configured".</summary>
    public string HeyGenApiKey { get; set; } = string.Empty;

    /// <summary>Whether a local lip-sync model (MuseTalk/LatentSync/SadTalker/LivePortrait) is installed.</summary>
    public bool LocalLipSyncInstalled { get; set; }

    /// <summary>Whether to emit a deterministic placeholder video asset when no model is installed (dev only).</summary>
    public bool SyntheticFallback { get; set; }

    /// <summary>Whether the Premium HeyGen tier has a usable API key.</summary>
    public bool HeyGenConfigured => !string.IsNullOrWhiteSpace(HeyGenApiKey);

    /// <summary>The actionable message surfaced when no local lip-sync model is installed.</summary>
    public const string LocalNotInstalledMessage =
        "Lip-sync model not installed — install a local model (MuseTalk / LatentSync / SadTalker / LivePortrait) " +
        "under Admin > Models for the Fast tier, or route this job to the High (RunPod) or Premium (HeyGen) tier.";

    /// <summary>The actionable message surfaced when the HeyGen Premium tier has no API key.</summary>
    public const string HeyGenNotConfiguredMessage =
        "HeyGen not configured — add HeyGen:ApiKey (Avatar:HeyGenApiKey) under Admin, or run this avatar job on " +
        "the Fast (local) or High (RunPod) tier.";

    /// <summary>
    /// Builds options from configuration, reading the <c>Avatar</c> section and the conventional
    /// <c>HeyGen:ApiKey</c> key-by-key so the engine needs no configuration-binder package.
    /// </summary>
    /// <param name="aConfiguration">The application configuration.</param>
    /// <returns>The populated options instance.</returns>
    public static AvatarOptions FromConfiguration(IConfiguration aConfiguration)
    {
        var vOptions = new AvatarOptions
        {
            HeyGenApiKey = aConfiguration["Avatar:HeyGenApiKey"] ?? aConfiguration["HeyGen:ApiKey"] ?? string.Empty
        };
        if (bool.TryParse(aConfiguration["Avatar:LocalLipSyncInstalled"], out var vLocal)) { vOptions.LocalLipSyncInstalled = vLocal; }
        if (bool.TryParse(aConfiguration["Avatar:SyntheticFallback"], out var vSynthetic)) { vOptions.SyntheticFallback = vSynthetic; }
        return vOptions;
    }
}
