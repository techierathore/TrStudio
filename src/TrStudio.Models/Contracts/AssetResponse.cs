namespace TrStudio.Models.Contracts;

/// <summary>
/// Response body describing a generated asset for gallery/history listings.
/// </summary>
public class AssetResponse
{
    /// <summary>The asset identifier.</summary>
    public Guid AssetId { get; set; }

    /// <summary>The job that produced the asset.</summary>
    public Guid JobId { get; set; }

    /// <summary>The media kind: <c>image</c>, <c>video</c> or <c>audio</c>.</summary>
    public string Kind { get; set; } = "image";

    /// <summary>Storage path/key of the asset binary.</summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>Storage path/key of the thumbnail rendition, if generated.</summary>
    public string? ThumbnailPath { get; set; }

    /// <summary>Pixel width for visual assets, if known.</summary>
    public int? Width { get; set; }

    /// <summary>Pixel height for visual assets, if known.</summary>
    public int? Height { get; set; }

    /// <summary>The registry model id that generated the asset.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>The project the asset belongs to, if any.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>The random seed used, for reproducibility.</summary>
    public long? SeedUsed { get; set; }

    /// <summary>UTC timestamp when the asset was persisted.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>The provider kind that produced the asset (e.g. "Venice"), from the generation log; null when unknown.</summary>
    public string? ProviderKind { get; set; }

    /// <summary>The run's recorded price in US dollars, from the generation log; null for free or unknown runs.</summary>
    public decimal? ActualCost { get; set; }

    /// <summary>Where the price came from ("venice-quote" / "venice-list"); null for estimate-priced or free runs.</summary>
    public string? PriceSource { get; set; }

    /// <summary>The provider's own failure text for the run, when it failed (e.g. "Venice: ..."); null on success.</summary>
    public string? FailureMessage { get; set; }
}
