using System.Text.Json;
using TrStudio.Engine.Domain.Image;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Entities;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Domain.Venice;

/// <summary>
/// Reads a creator's own pictures as data URLs for Venice (local assets are never served by
/// address, ADR-014) and stores Venice's output as a generated asset. Every read is owner-scoped
/// (BRD-81).
/// </summary>
public class VeniceAssetIo
{
    private readonly IAssetRepository objAssetRepository;
    private readonly ICharacterRepository objCharacterRepository;
    private readonly IAssetStorage objAssetStorage;
    private readonly IImageProcessor objImageProcessor;

    /// <summary>
    /// Initialises the reader/writer.
    /// </summary>
    /// <param name="aAssetRepository">Asset metadata (owner-scoped).</param>
    /// <param name="aCharacterRepository">Characters (owner-scoped) holding the fixed-character reference set.</param>
    /// <param name="aAssetStorage">The local object store.</param>
    /// <param name="aImageProcessor">Reads a stored picture's size.</param>
    public VeniceAssetIo(
        IAssetRepository aAssetRepository,
        ICharacterRepository aCharacterRepository,
        IAssetStorage aAssetStorage,
        IImageProcessor aImageProcessor)
    {
        objAssetRepository = aAssetRepository;
        objCharacterRepository = aCharacterRepository;
        objAssetStorage = aAssetStorage;
        objImageProcessor = aImageProcessor;
    }

    /// <summary>Loads the creator's own assets as (data URL, raw base64) pairs, in order.</summary>
    /// <param name="aAssetIds">The asset ids.</param>
    /// <param name="aUserId">The owner.</param>
    /// <param name="aCancellationToken">Token to cancel the reads.</param>
    /// <returns>The encoded pictures.</returns>
    public async Task<IReadOnlyList<(string DataUrl, string Base64)>> LoadAsync(IEnumerable<Guid> aAssetIds, string aUserId, CancellationToken aCancellationToken)
    {
        var vResult = new List<(string, string)>();
        foreach (var vId in aAssetIds)
        {
            var vAsset = await objAssetRepository.GetAsync(vId, aUserId, aCancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Input picture '{vId}' was not found for this user.");
            vResult.Add(await EncodeFileAsync(vAsset.StoragePath, aCancellationToken).ConfigureAwait(false));
        }

        return vResult;
    }

    /// <summary>
    /// Loads a character's reference set as data URLs (asset ids, stored files) or passes web
    /// addresses through, for <c>reference_image_urls</c> (fixed character).
    /// </summary>
    /// <param name="aCharacterId">The character.</param>
    /// <param name="aUserId">The owner.</param>
    /// <param name="aCancellationToken">Token to cancel the reads.</param>
    /// <returns>The reference pictures.</returns>
    public async Task<IReadOnlyList<string>> LoadCharacterReferencesAsync(Guid aCharacterId, string aUserId, CancellationToken aCancellationToken)
    {
        var vCharacter = await objCharacterRepository.GetAsync(aCharacterId, aUserId, aCancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Character '{aCharacterId}' was not found for this user.");

        var vRefs = JsonSerializer.Deserialize<List<string>>(string.IsNullOrWhiteSpace(vCharacter.ReferenceAssetsJson) ? "[]" : vCharacter.ReferenceAssetsJson)
            ?? new List<string>();
        var vResult = new List<string>();
        foreach (var vRef in vRefs.Where(aRef => !string.IsNullOrWhiteSpace(aRef)))
        {
            if (Guid.TryParse(vRef, out var vAssetId))
            {
                vResult.Add((await LoadAsync(new[] { vAssetId }, aUserId, aCancellationToken).ConfigureAwait(false))[0].DataUrl);
            }
            else if (vRef.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || vRef.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                     || vRef.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                vResult.Add(vRef);
            }
            else if (File.Exists(vRef))
            {
                vResult.Add((await EncodeFileAsync(vRef, aCancellationToken).ConfigureAwait(false)).DataUrl);
            }
        }

        return vResult;
    }

    /// <summary>Stores Venice's output and inserts its asset row.</summary>
    /// <param name="aJob">The job.</param>
    /// <param name="aBytes">The output bytes.</param>
    /// <param name="aKind"><c>image</c> or <c>video</c>.</param>
    /// <param name="aCancellationToken">Token to cancel the write.</param>
    /// <returns>The asset reference.</returns>
    public async Task<AssetReference> SaveAsync(GenerationJob aJob, byte[] aBytes, string aKind, CancellationToken aCancellationToken)
    {
        var vExtension = aKind == "video" ? "mp4" : SniffImageExtension(aBytes);
        var vFolder = SanitizeForPath(aJob.UserId);
        var vPath = await objAssetStorage.SaveAsync(Path.Combine(vFolder, "venice", $"{aJob.JobId}.{vExtension}"), aBytes, aCancellationToken).ConfigureAwait(false);

        int? vWidth = null;
        int? vHeight = null;
        if (aKind == "image")
        {
            try
            {
                (vWidth, vHeight) = objImageProcessor.Probe(aBytes);
            }
            catch (Exception)
            {
                // A picture ImageSharp cannot read still gets stored; only its size is unknown.
            }
        }

        var vAsset = new GeneratedAsset
        {
            AssetId = Guid.NewGuid(),
            JobId = aJob.JobId,
            UserId = aJob.UserId,
            ProjectId = aJob.ProjectId,
            Kind = aKind,
            StoragePath = vPath,
            ThumbnailPath = aKind == "image" ? vPath : null,
            Width = vWidth,
            Height = vHeight,
            SeedUsed = aJob.SeedUsed,
            ParamsJson = aJob.ParamsJson,
            ModelId = aJob.ModelId,
            ProvenanceJson = JsonSerializer.Serialize(new { Generator = "TrStudio VeniceProvider", Provider = "Venice", aJob.ModelId, aJob.JobId, CreatedUtc = DateTime.UtcNow }),
            CreatedUtc = DateTime.UtcNow
        };
        await objAssetRepository.InsertAsync(vAsset, aCancellationToken).ConfigureAwait(false);
        return new AssetReference(vAsset.AssetId, vAsset.StoragePath, vAsset.Kind);
    }

    private static async Task<(string DataUrl, string Base64)> EncodeFileAsync(string aPath, CancellationToken aCancellationToken)
    {
        if (!File.Exists(aPath))
        {
            throw new InvalidOperationException("An input picture's file is missing from storage.");
        }

        var vBytes = await File.ReadAllBytesAsync(aPath, aCancellationToken).ConfigureAwait(false);
        return (VeniceRequestBuilder.ToMediaDataUrl(vBytes, aPath), Convert.ToBase64String(vBytes));
    }

    private static string SniffImageExtension(byte[] aBytes)
    {
        if (aBytes.Length > 3 && aBytes[0] == 0xFF && aBytes[1] == 0xD8)
        {
            return "jpg";
        }

        if (aBytes.Length > 12 && aBytes[0] == (byte)'R' && aBytes[1] == (byte)'I' && aBytes[8] == (byte)'W' && aBytes[9] == (byte)'E')
        {
            return "webp";
        }

        return "png";
    }

    private static string SanitizeForPath(string aUserId)
    {
        var vInvalid = Path.GetInvalidFileNameChars();
        var vClean = new string(aUserId.Select(aChar => vInvalid.Contains(aChar) ? '_' : aChar).ToArray());
        return string.IsNullOrWhiteSpace(vClean) ? "unknown" : vClean;
    }
}
