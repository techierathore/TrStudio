namespace TrStudio.Models.Contracts;

/// <summary>
/// Requests a change to the owner-only Unrestricted (NSFW) mode (F-NSFW / BRD-79). Enabling is
/// refused in multi-user/SaaS mode; the non-disableable floors are never affected.
/// </summary>
public class NsfwPolicyUpdateRequest
{
    /// <summary>Whether the owner wants Unrestricted (NSFW) mode enabled.</summary>
    public bool Enabled { get; set; }
}
