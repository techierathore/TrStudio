namespace TrStudio.Models.Contracts;

/// <summary>
/// Response for a video post-processing step (FN-053). Reports the step performed, whether it
/// degraded (model not installed) with an actionable message, and the produced asset id when the
/// step ran for real.
/// </summary>
public class VideoPostStepResponse
{
    /// <summary>The post-step performed: <c>interpolate</c> or <c>upscale</c>.</summary>
    public string Step { get; set; } = string.Empty;

    /// <summary>Outcome token: <c>ok</c>, <c>degraded</c> or <c>error</c>.</summary>
    public string Status { get; set; } = "ok";

    /// <summary>Whether the step degraded cleanly because its model is not installed.</summary>
    public bool Degraded { get; set; }

    /// <summary>An actionable human-readable message describing the outcome/degrade.</summary>
    public string? Message { get; set; }

    /// <summary>The produced video asset id, when the step ran for real.</summary>
    public Guid? OutputAssetId { get; set; }
}
