using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Domain.Export;

/// <summary>
/// Default <see cref="IAssetExportService"/> (REQ-FN-067/BRD-51). Builds an in-memory zip: each
/// owned asset is placed under its own folder alongside a <c>provenance.json</c> sidecar carrying
/// the stored C2PA-style manifest (SAFE-04), and a top-level <c>manifest.json</c> indexes the
/// package. Every asset read is owner-filtered (BRD-81); asset binaries are read from the local
/// object store by their recorded storage path.
/// </summary>
public class AssetExportService : IAssetExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IAssetRepository objAssetRepository;
    private readonly ILogger<AssetExportService> objLogger;

    /// <summary>
    /// Initialises the service.
    /// </summary>
    /// <param name="aAssetRepository">Owner-filtered asset metadata access.</param>
    /// <param name="aLogger">Structured logger.</param>
    public AssetExportService(IAssetRepository aAssetRepository, ILogger<AssetExportService> aLogger)
    {
        objAssetRepository = aAssetRepository;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<AssetExportResult?> ExportSingleAsync(Guid aAssetId, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vAsset = await objAssetRepository.GetAsync(aAssetId, aUserId, aCancellationToken).ConfigureAwait(false);
        if (vAsset is null)
        {
            return null;
        }

        return await BuildZipAsync(new[] { vAsset }, $"trstudio-asset-{aAssetId}.zip", aCancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AssetExportResult> ExportBulkAsync(IReadOnlyList<Guid> aAssetIds, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vAssets = new List<GeneratedAsset>();
        foreach (var vAssetId in aAssetIds.Distinct())
        {
            var vAsset = await objAssetRepository.GetAsync(vAssetId, aUserId, aCancellationToken).ConfigureAwait(false);
            if (vAsset is not null)
            {
                vAssets.Add(vAsset);
            }
        }

        var vFileName = $"trstudio-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
        return await BuildZipAsync(vAssets, vFileName, aCancellationToken).ConfigureAwait(false);
    }

    private async Task<AssetExportResult> BuildZipAsync(IReadOnlyList<GeneratedAsset> aAssets, string aFileName, CancellationToken aCancellationToken)
    {
        var vProvenanceCount = 0;
        var vManifestEntries = new List<object>();

        using var vMemory = new MemoryStream();
        using (var vArchive = new ZipArchive(vMemory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var vAsset in aAssets)
            {
                var vFolder = vAsset.AssetId.ToString();

                // The binary, when present in the local object store.
                var vBinaryName = SafeFileName(vAsset.StoragePath, $"{vAsset.AssetId}.bin");
                var vHasBinary = !string.IsNullOrWhiteSpace(vAsset.StoragePath) && File.Exists(vAsset.StoragePath);
                if (vHasBinary)
                {
                    var vBytes = await File.ReadAllBytesAsync(vAsset.StoragePath, aCancellationToken).ConfigureAwait(false);
                    var vBinaryEntry = vArchive.CreateEntry($"{vFolder}/{vBinaryName}", CompressionLevel.Optimal);
                    await using var vBinaryStream = vBinaryEntry.Open();
                    await vBinaryStream.WriteAsync(vBytes, aCancellationToken).ConfigureAwait(false);
                }

                // The provenance sidecar — the C2PA-style manifest travels WITH the asset (SAFE-04).
                var vProvenance = string.IsNullOrWhiteSpace(vAsset.ProvenanceJson)
                    ? "{\"warning\":\"No provenance manifest was recorded for this asset.\"}"
                    : vAsset.ProvenanceJson;
                if (!string.IsNullOrWhiteSpace(vAsset.ProvenanceJson))
                {
                    vProvenanceCount++;
                }

                await WriteTextEntryAsync(vArchive, $"{vFolder}/provenance.json", vProvenance, aCancellationToken).ConfigureAwait(false);

                vManifestEntries.Add(new
                {
                    assetId = vAsset.AssetId,
                    jobId = vAsset.JobId,
                    kind = vAsset.Kind,
                    modelId = vAsset.ModelId,
                    file = vHasBinary ? $"{vFolder}/{vBinaryName}" : null,
                    provenance = $"{vFolder}/provenance.json",
                    hasProvenance = !string.IsNullOrWhiteSpace(vAsset.ProvenanceJson),
                    createdUtc = vAsset.CreatedUtc
                });
            }

            var vManifest = JsonSerializer.Serialize(new
            {
                generator = "TrStudio",
                exportedUtc = DateTime.UtcNow,
                assetCount = aAssets.Count,
                assets = vManifestEntries
            }, JsonOptions);
            await WriteTextEntryAsync(vArchive, "manifest.json", vManifest, aCancellationToken).ConfigureAwait(false);
        }

        objLogger.LogInformation("Built asset export {FileName} with {AssetCount} asset(s), {ProvenanceCount} with provenance",
            aFileName, aAssets.Count, vProvenanceCount);

        return new AssetExportResult
        {
            ZipBytes = vMemory.ToArray(),
            FileName = aFileName,
            AssetCount = aAssets.Count,
            ProvenanceCount = vProvenanceCount
        };
    }

    private static async Task WriteTextEntryAsync(ZipArchive aArchive, string aEntryName, string aContent, CancellationToken aCancellationToken)
    {
        var vEntry = aArchive.CreateEntry(aEntryName, CompressionLevel.Optimal);
        await using var vStream = vEntry.Open();
        await vStream.WriteAsync(Encoding.UTF8.GetBytes(aContent), aCancellationToken).ConfigureAwait(false);
    }

    private static string SafeFileName(string? aStoragePath, string aFallback)
    {
        if (string.IsNullOrWhiteSpace(aStoragePath))
        {
            return aFallback;
        }

        var vName = Path.GetFileName(aStoragePath);
        return string.IsNullOrWhiteSpace(vName) ? aFallback : vName;
    }
}
