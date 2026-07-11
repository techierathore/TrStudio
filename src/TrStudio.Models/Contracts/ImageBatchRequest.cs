using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for submitting a batch/grid of image variations with deterministic seed control
/// (<c>POST /api/v1/images/batch</c>, REQ-FN-023). The batch submits <see cref="Count"/> jobs
/// over the identical model/params, one per seed in the contiguous range
/// [<see cref="BaseSeed"/>, <see cref="BaseSeed"/> + <see cref="Count"/>), so re-running the
/// same request reproduces the identical grid (same seed + params → identical output).
/// </summary>
public class ImageBatchRequest
{
    /// <summary>The registry model id to run for every variation.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>The generation task to perform; must match the model's declared task.</summary>
    public GenerationTaskType Task { get; set; }

    /// <summary>The execution tier selected for every variation.</summary>
    public GenerationTier Tier { get; set; }

    /// <summary>The shared job parameters as a JSON document matching the model's param specs.</summary>
    public string ParamsJson { get; set; } = "{}";

    /// <summary>The first seed of the contiguous variation range (reproducibility anchor).</summary>
    public long BaseSeed { get; set; }

    /// <summary>The number of variations to generate (clamped to 1–16).</summary>
    public int Count { get; set; } = 4;

    /// <summary>The project to attach every variation to, if any.</summary>
    public Guid? ProjectId { get; set; }
}
