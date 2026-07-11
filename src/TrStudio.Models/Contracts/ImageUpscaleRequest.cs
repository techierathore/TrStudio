namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for upscaling a generated image to 2K/4K with optional face restoration
/// (<c>POST /api/v1/images/upscale</c>, REQ-FN-022). Operates on an owner-scoped source asset
/// and persists a new derived asset.
/// </summary>
public class ImageUpscaleRequest
{
    /// <summary>The owner-scoped source asset to upscale.</summary>
    public Guid SourceAssetId { get; set; }

    /// <summary>
    /// The target long-edge resolution in pixels. Clamped to the supported 2K/4K band
    /// (1024–4096); typical values are 2048 (2K) and 4096 (4K).
    /// </summary>
    public int TargetLongEdge { get; set; } = 2048;

    /// <summary>Whether to apply a face-restoration pass (detail-preserving sharpen) after upscaling.</summary>
    public bool FaceRestore { get; set; }
}
