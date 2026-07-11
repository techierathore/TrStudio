using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// Response for a video generation submission (FN-051/052). Reports which tier-routed registry model
/// and concrete provider the SAME job spec resolved to, whether that provider will degrade cleanly
/// (no model / no aggregator key), the accepted job id and the pre-run cost estimate + spend-cap
/// warning. A <see cref="Status"/> of <c>blocked</c> indicates a non-disableable safety floor refused
/// the request (FN-063 / SAFE-01..06).
/// </summary>
public class VideoGenerateResponse
{
    /// <summary>The resolved generation mode/task for this job.</summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>The requested execution tier.</summary>
    public GenerationTier Tier { get; set; }

    /// <summary>The tier-routed registry model id the job resolved to.</summary>
    public string ResolvedModelId { get; set; } = string.Empty;

    /// <summary>The resolved provider kind (FN-051 tier-routing evidence).</summary>
    public ProviderKind ResolvedProviderKind { get; set; }

    /// <summary>The concrete provider implementation type name the job resolved to.</summary>
    public string? ResolvedProvider { get; set; }

    /// <summary>Whether the resolved provider will degrade cleanly (no model/key) at execution.</summary>
    public bool WillDegrade { get; set; }

    /// <summary>The accepted job id, when the submission was queued.</summary>
    public Guid? JobId { get; set; }

    /// <summary>Outcome token: <c>queued</c>, <c>blocked</c> or <c>error</c>.</summary>
    public string Status { get; set; } = "queued";

    /// <summary>An actionable human-readable message describing the outcome/degrade.</summary>
    public string? Message { get; set; }

    /// <summary>The pre-run cost estimate in the account currency, if the tier is billed.</summary>
    public decimal? CostEstimate { get; set; }

    /// <summary>A spend-cap near/over warning surfaced by the guardrail, if any.</summary>
    public string? Warning { get; set; }
}
