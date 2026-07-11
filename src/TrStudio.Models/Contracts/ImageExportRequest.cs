namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for exporting a generated image into a social-format preset
/// (<c>POST /api/v1/images/export</c>, REQ-FN-024). Operates on an owner-scoped source asset,
/// centre-crops it to the preset's aspect ratio and persists a new derived asset.
/// </summary>
public class ImageExportRequest
{
    /// <summary>The owner-scoped source asset to export.</summary>
    public Guid SourceAssetId { get; set; }

    /// <summary>
    /// The social-format aspect preset: <c>1:1</c> (square), <c>4:5</c> (portrait),
    /// <c>9:16</c> (story/reel) or <c>16:9</c> (landscape).
    /// </summary>
    public string Preset { get; set; } = "1:1";
}
