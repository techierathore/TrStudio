using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// Result of a talking-avatar submission (FN-037/039). Surfaces the durable job id plus the
/// tier-resolved routing — the registry model, its provider kind and the concrete provider type —
/// so callers can prove that ONE tier control swaps the provider for the same job spec (FN-039).
/// When no lip-sync model/key is configured the job still submits and degrades cleanly at execution.
/// </summary>
public class AvatarTalkingResponse
{
    /// <summary>The durable generation-job id (poll <c>/api/v1/jobs/{id}</c> for progress).</summary>
    public Guid JobId { get; set; }

    /// <summary>The character the avatar was built for.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>The requested tier.</summary>
    public GenerationTier Tier { get; set; }

    /// <summary>The registry model the tier resolved to (distinct per tier).</summary>
    public string ResolvedModelId { get; set; } = string.Empty;

    /// <summary>The provider kind the tier resolved to (Fast→ComfyUi, High→RunPod, Premium→HeyGen).</summary>
    public ProviderKind ResolvedProviderKind { get; set; }

    /// <summary>The concrete provider type name resolved by the registry (FN-039 evidence).</summary>
    public string ResolvedProvider { get; set; } = string.Empty;

    /// <summary>Submission status: <c>queued</c>, <c>blocked</c> or <c>error</c>.</summary>
    public string Status { get; set; } = "queued";

    /// <summary>Whether the tier's provider is unconfigured, so execution will degrade gracefully.</summary>
    public bool WillDegrade { get; set; }

    /// <summary>An actionable message describing the routing / degrade / block outcome.</summary>
    public string? Message { get; set; }
}
