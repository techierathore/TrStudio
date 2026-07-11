namespace TrStudio.Models.Contracts;

/// <summary>
/// Result of probing a model source's base URL for reachability
/// (<c>POST /api/v1/admin/sources/{id}/test-connection</c>, REQ-FN-030). No token value is ever
/// echoed in the result.
/// </summary>
public class SourceTestConnectionResponse
{
    /// <summary>The source that was probed.</summary>
    public Guid SourceId { get; set; }

    /// <summary>Whether the base URL responded (any HTTP status counts as reachable).</summary>
    public bool Reachable { get; set; }

    /// <summary>The HTTP status code returned by the probe, when the host responded.</summary>
    public int? HttpStatus { get; set; }

    /// <summary>Short status label persisted onto the source (e.g. "ok", "unreachable").</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Human-readable detail describing the probe outcome.</summary>
    public string Message { get; set; } = string.Empty;
}
