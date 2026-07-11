namespace TrStudio.Models.Contracts;

/// <summary>
/// Response describing a submitted batch/grid of image variations (REQ-FN-023). Each entry
/// pairs a submitted job with the exact seed that reproduces it.
/// </summary>
public class ImageBatchResponse
{
    /// <summary>The submitted variations, one per seed, in seed order.</summary>
    public List<ImageBatchItem> Items { get; set; } = new();
}

/// <summary>
/// One submitted variation within a batch: the durable job id and the seed that reproduces it.
/// </summary>
public class ImageBatchItem
{
    /// <summary>The submitted generation job id.</summary>
    public Guid JobId { get; set; }

    /// <summary>The exact seed used for this variation (reproducibility key).</summary>
    public long Seed { get; set; }

    /// <summary>The pre-flight cost estimate for this variation; null for free local execution.</summary>
    public decimal? CostEstimate { get; set; }
}
