namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for removing the background of a generated image, producing an RGBA asset with
/// a transparent background (<c>POST /api/v1/images/background-remove</c>, REQ-FN-024).
/// Operates on an owner-scoped source asset and persists a new derived asset.
/// </summary>
public class ImageBackgroundRemovalRequest
{
    /// <summary>The owner-scoped source asset whose background is removed.</summary>
    public Guid SourceAssetId { get; set; }

    /// <summary>
    /// The colour-distance tolerance (0–255) for treating an edge-connected pixel as background.
    /// Higher values remove more aggressively.
    /// </summary>
    public int Tolerance { get; set; } = 32;
}
