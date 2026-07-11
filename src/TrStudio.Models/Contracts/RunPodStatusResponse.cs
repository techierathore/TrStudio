namespace TrStudio.Models.Contracts;

/// <summary>
/// RunPod serverless status returned by the admin ops endpoints (REQ-FN-028/BRD-12). When
/// RunPod is not configured, <see cref="Configured"/> is false and <see cref="Message"/> carries
/// an actionable remediation instead of a crash (graceful degrade).
/// </summary>
public class RunPodStatusResponse
{
    /// <summary>Whether a RunPod API key and endpoint id are configured.</summary>
    public bool Configured { get; set; }

    /// <summary>Coarse worker state label: <c>not-configured</c>, <c>idle</c>, <c>ready</c>, <c>running</c> or <c>cold</c>.</summary>
    public string WorkerState { get; set; } = "not-configured";

    /// <summary>Whether the endpoint has scaled to zero and the next job pays a cold-start penalty.</summary>
    public bool ColdStart { get; set; }

    /// <summary>Whether the auto idle-stop (scale-to-zero when idle) toggle is enabled.</summary>
    public bool AutoIdleStop { get; set; }

    /// <summary>The configured serverless endpoint id, when present.</summary>
    public string? EndpointId { get; set; }

    /// <summary>Human-readable status or remediation message.</summary>
    public string Message { get; set; } = string.Empty;
}
