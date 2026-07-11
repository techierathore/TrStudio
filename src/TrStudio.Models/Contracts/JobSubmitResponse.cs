namespace TrStudio.Models.Contracts;

/// <summary>
/// Response body returned after a generation job is accepted and enqueued.
/// </summary>
public class JobSubmitResponse
{
    /// <summary>The identifier of the enqueued job.</summary>
    public Guid JobId { get; set; }

    /// <summary>Pre-run cost estimate in the account currency; null for free local execution.</summary>
    public decimal? CostEstimate { get; set; }

    /// <summary>Optional warning surfaced to the UI (e.g. routing to a closed API, near spend cap).</summary>
    public string? Warning { get; set; }
}
