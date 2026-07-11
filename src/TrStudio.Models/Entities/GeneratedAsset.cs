namespace TrStudio.Models.Entities;

/// <summary>
/// Metadata for a generated media asset persisted after a successful job
/// (the binary lives in object storage; only metadata is stored in Postgres).
/// </summary>
public class GeneratedAsset
{
    /// <summary>Unique asset identifier.</summary>
    public Guid AssetId { get; set; }

    /// <summary>The job that produced this asset.</summary>
    public Guid JobId { get; set; }

    /// <summary>The AppManager user id owning this asset (per-user isolation, BRD-81).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The project this asset belongs to, if any.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>The media kind: <c>image</c>, <c>video</c> or <c>audio</c>.</summary>
    public string Kind { get; set; } = "image";

    /// <summary>Storage path/key of the asset binary in object storage.</summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>Storage path/key of the thumbnail rendition, if generated.</summary>
    public string? ThumbnailPath { get; set; }

    /// <summary>Pixel width for visual assets, if known.</summary>
    public int? Width { get; set; }

    /// <summary>Pixel height for visual assets, if known.</summary>
    public int? Height { get; set; }

    /// <summary>The random seed used to generate the asset, for reproducibility.</summary>
    public long? SeedUsed { get; set; }

    /// <summary>The job parameters JSON snapshot the asset was generated with.</summary>
    public string ParamsJson { get; set; } = "{}";

    /// <summary>The registry model id that generated the asset.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>C2PA-style provenance metadata JSON stamped on the output (SAFE-04), if present.</summary>
    public string? ProvenanceJson { get; set; }

    /// <summary>UTC timestamp when the asset was persisted.</summary>
    public DateTime CreatedUtc { get; set; }
}
