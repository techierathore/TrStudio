using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// The immutable context the safety pipeline evaluates for one generation (SAFE-01..06). Built by
/// the orchestrator (pre-check) and the worker (post-check) so the identical floors run on every
/// provider, on both the input and the output.
/// </summary>
public class SafetyEvaluationContext
{
    /// <summary>The job identifier (a pre-check block may reference a not-yet-enqueued job).</summary>
    public Guid JobId { get; set; }

    /// <summary>The owning AppManager user id (owner filter, BRD-81).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The registry model id the generation targets.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>The generation task requested.</summary>
    public GenerationTaskType Task { get; set; }

    /// <summary>The execution tier requested.</summary>
    public GenerationTier Tier { get; set; }

    /// <summary>The job parameters JSON — the safety pipeline parses its signals (prompt, real-person flags, ...).</summary>
    public string ParamsJson { get; set; } = "{}";

    /// <summary>The model's safety classification (synthetic-default vs real-person-gated).</summary>
    public SafetyClass SafetyClass { get; set; }
}
