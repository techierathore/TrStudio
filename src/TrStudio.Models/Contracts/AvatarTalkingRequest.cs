using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// Request to produce a talking avatar for a character (FN-037/BRD-28): lip-sync from the character's
/// locked identity plus a script and/or a pre-synthesised audio asset. The <see cref="Tier"/> is the
/// single control that swaps the lip-sync provider underneath (FN-039) — Fast (local), High (RunPod)
/// or Premium (HeyGen) — for the identical job spec.
/// </summary>
public class AvatarTalkingRequest
{
    /// <summary>The character whose locked identity drives the avatar (required).</summary>
    public Guid CharacterId { get; set; }

    /// <summary>The spoken script; used when no pre-synthesised audio asset is supplied.</summary>
    public string? Script { get; set; }

    /// <summary>Optional pre-synthesised audio asset id (e.g. from the Voice vertical, FN-035).</summary>
    public Guid? AudioAssetId { get; set; }

    /// <summary>Target language (<c>en</c>/<c>hi</c>/<c>hinglish</c>); defaults to <c>en</c>.</summary>
    public string Language { get; set; } = "en";

    /// <summary>The single tier control selecting the lip-sync provider (FN-039). Defaults to Fast.</summary>
    public GenerationTier Tier { get; set; } = GenerationTier.Fast;

    /// <summary>Optional owning project id (audit/association only).</summary>
    public Guid? ProjectId { get; set; }
}
