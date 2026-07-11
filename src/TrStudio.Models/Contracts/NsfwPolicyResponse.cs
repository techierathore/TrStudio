namespace TrStudio.Models.Contracts;

/// <summary>
/// The effective state of the owner-only Unrestricted (NSFW) mode (F-NSFW / BRD-79). The mode is
/// only ever EFFECTIVE in personal/single-user mode; it is forced off in multi-user/SaaS. The
/// non-disableable floors (SAFE-01..06) remain enforced regardless of this state.
/// </summary>
public class NsfwPolicyResponse
{
    /// <summary>The stored owner setting (what the admin last requested).</summary>
    public bool StoredNsfwEnabled { get; set; }

    /// <summary>Whether the deployment is in personal/single-user mode.</summary>
    public bool PersonalMode { get; set; }

    /// <summary>The effective mode: <c>StoredNsfwEnabled AND PersonalMode</c>. Multi-user always resolves to false.</summary>
    public bool EffectiveNsfwEnabled { get; set; }

    /// <summary>A human-readable explanation of the effective state (e.g. why it is forced off).</summary>
    public string Message { get; set; } = string.Empty;
}
