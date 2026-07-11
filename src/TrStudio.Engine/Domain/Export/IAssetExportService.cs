namespace TrStudio.Engine.Domain.Export;

/// <summary>
/// Packages owned generated assets for download WITH their C2PA-style provenance manifests intact
/// (REQ-FN-067/BRD-51). Single and bulk exports both produce a zip whose every asset carries its
/// provenance sidecar so downstream disclosure survives the download (SAFE-04). All reads are
/// owner-filtered (BRD-81).
/// </summary>
public interface IAssetExportService
{
    /// <summary>
    /// Exports a single owned asset as a zip (binary + provenance sidecar + manifest).
    /// </summary>
    /// <param name="aAssetId">The asset identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The export package, or null when the asset is not found or not owned.</returns>
    Task<AssetExportResult?> ExportSingleAsync(Guid aAssetId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Exports a set of owned assets as a single zip. Ids that are not found or not owned are
    /// silently skipped (never leaked); the result reports how many were included.
    /// </summary>
    /// <param name="aAssetIds">The asset identifiers to include.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The export package (may include zero assets when none were owned/found).</returns>
    Task<AssetExportResult> ExportBulkAsync(IReadOnlyList<Guid> aAssetIds, string aUserId, CancellationToken aCancellationToken = default);
}

/// <summary>The result of an asset export: the zip bytes plus what went into it.</summary>
public sealed class AssetExportResult
{
    /// <summary>The zip archive bytes to stream to the caller.</summary>
    public byte[] ZipBytes { get; init; } = Array.Empty<byte>();

    /// <summary>The suggested download file name.</summary>
    public string FileName { get; init; } = "trstudio-export.zip";

    /// <summary>How many assets were packaged.</summary>
    public int AssetCount { get; init; }

    /// <summary>How many of the packaged assets carried a provenance manifest.</summary>
    public int ProvenanceCount { get; init; }
}
