using System.Security.Claims;
using System.Text.Json;
using TrStudio.Engine.Domain.Image;
using TrStudio.Engine.Domain.Jobs;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudioApi.Endpoints;

/// <summary>
/// Endpoint mapping for the image cluster (<c>/api/v1/images</c>): image-vertical operations
/// layered over the shared job pipeline and the deterministic <see cref="IImageProcessor"/>.
/// Covers input uploads for inpaint/ControlNet (REQ-FN-020/021), seed-controlled batch/grid
/// variations (REQ-FN-023), 2K/4K upscale + face restoration (REQ-FN-022), social-format export
/// presets and background removal (REQ-FN-024). EVERY handler is owner-scoped (BRD-81).
/// </summary>
/// <remarks>
/// OWNED BY: cluster F (image). Only that cluster edits this file; other clusters map their
/// endpoints in their own file. Text-to-image / image-to-image / inpaint generation itself runs
/// through the spine's <c>POST /api/v1/jobs</c> against an <c>OnnxLocal</c> model (REQ-FN-019).
/// </remarks>
public static class ImageEndpoints
{
    /// <summary>The image-vertical route prefix.</summary>
    private const string ImagesRoute = "/api/v1/images";

    /// <summary>
    /// Maps the image endpoints onto the application pipeline.
    /// </summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapImageEndpoints(this WebApplication aApp)
    {
        var vImages = aApp.MapGroup(ImagesRoute).WithTags("Images");

        vImages.MapPost("uploads", UploadInputAsync).WithName("UploadImageInput");
        vImages.MapPost("batch", SubmitBatchAsync).WithName("SubmitImageBatch");
        vImages.MapPost("upscale", UpscaleAsync).WithName("UpscaleImage");
        vImages.MapPost("export", ExportAsync).WithName("ExportImage");
        vImages.MapPost("background-remove", RemoveBackgroundAsync).WithName("RemoveImageBackground");

        return aApp;
    }

    // ---- Input upload: mask / reference / ControlNet conditioning (REQ-FN-020/021) --------

    private static async Task<IResult> UploadInputAsync(
        ImageUploadRequest aRequest,
        ClaimsPrincipal aUser,
        IAssetStorage aAssetStorage,
        IAssetRepository aAssetRepository,
        IImageProcessor aImageProcessor,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        byte[] vBytes;
        try
        {
            vBytes = Convert.FromBase64String(aRequest.Base64Content ?? string.Empty);
        }
        catch (FormatException)
        {
            return Results.BadRequest(new { error = "Base64Content is not valid base64." });
        }

        if (vBytes.Length == 0)
        {
            return Results.BadRequest(new { error = "Base64Content is empty." });
        }

        int? vWidth = null;
        int? vHeight = null;
        try
        {
            var vDimensions = aImageProcessor.Probe(vBytes);
            vWidth = vDimensions.Width;
            vHeight = vDimensions.Height;
        }
        catch (Exception)
        {
            return Results.BadRequest(new { error = "The uploaded bytes are not a decodable image." });
        }

        var vKind = string.IsNullOrWhiteSpace(aRequest.Kind) ? "mask" : aRequest.Kind.Trim();
        var vResponse = await PersistDerivedAssetAsync(
            aAssetStorage, aAssetRepository, vUserId, vBytes, vKind, $"image-upload:{vKind}",
            aRequest.ProjectId, vWidth, vHeight, aCancellationToken);
        return Results.Ok(vResponse);
    }

    // ---- Batch/grid variations with deterministic seeds (REQ-FN-023) ----------------------

    private static async Task<IResult> SubmitBatchAsync(
        ImageBatchRequest aRequest,
        ClaimsPrincipal aUser,
        IJobOrchestrator aOrchestrator,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vCount = Math.Clamp(aRequest.Count, 1, 16);
        var vResponse = new ImageBatchResponse();

        try
        {
            for (var vIndex = 0; vIndex < vCount; vIndex++)
            {
                var vSeed = aRequest.BaseSeed + vIndex;
                var vSubmit = new JobSubmitRequest
                {
                    ModelId = aRequest.ModelId,
                    Task = aRequest.Task,
                    Tier = aRequest.Tier,
                    ParamsJson = aRequest.ParamsJson,
                    ProjectId = aRequest.ProjectId,
                    Seed = vSeed
                };

                var vSubmitted = await aOrchestrator.SubmitAsync(vSubmit, vUserId, aCancellationToken);
                vResponse.Items.Add(new ImageBatchItem
                {
                    JobId = vSubmitted.JobId,
                    Seed = vSeed,
                    CostEstimate = vSubmitted.CostEstimate
                });
            }
        }
        catch (KeyNotFoundException vException)
        {
            return Results.NotFound(new { error = vException.Message });
        }
        catch (ArgumentException vException)
        {
            return Results.BadRequest(new { error = vException.Message });
        }

        return Results.Ok(vResponse);
    }

    // ---- Upscale + face restoration to 2K/4K (REQ-FN-022) ---------------------------------

    private static async Task<IResult> UpscaleAsync(
        ImageUpscaleRequest aRequest,
        ClaimsPrincipal aUser,
        IAssetStorage aAssetStorage,
        IAssetRepository aAssetRepository,
        IImageProcessor aImageProcessor,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vSource = await LoadOwnedSourceAsync(aAssetRepository, aRequest.SourceAssetId, vUserId, aCancellationToken);
        if (vSource is null)
        {
            return Results.NotFound(new { error = "Source asset not found." });
        }

        var vBytes = await aImageProcessor.UpscaleAsync(vSource.Bytes, aRequest.TargetLongEdge, aRequest.FaceRestore, aCancellationToken);
        var (vWidth, vHeight) = aImageProcessor.Probe(vBytes);
        var vResponse = await PersistDerivedAssetAsync(
            aAssetStorage, aAssetRepository, vUserId, vBytes, "image", "image-op:upscale",
            vSource.Asset.ProjectId, vWidth, vHeight, aCancellationToken);
        return Results.Ok(vResponse);
    }

    // ---- Social-format export presets (REQ-FN-024) ----------------------------------------

    private static async Task<IResult> ExportAsync(
        ImageExportRequest aRequest,
        ClaimsPrincipal aUser,
        IAssetStorage aAssetStorage,
        IAssetRepository aAssetRepository,
        IImageProcessor aImageProcessor,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vSource = await LoadOwnedSourceAsync(aAssetRepository, aRequest.SourceAssetId, vUserId, aCancellationToken);
        if (vSource is null)
        {
            return Results.NotFound(new { error = "Source asset not found." });
        }

        byte[] vBytes;
        try
        {
            vBytes = await aImageProcessor.ExportPresetAsync(vSource.Bytes, aRequest.Preset, aCancellationToken);
        }
        catch (ArgumentException vException)
        {
            return Results.BadRequest(new { error = vException.Message });
        }

        var (vWidth, vHeight) = aImageProcessor.Probe(vBytes);
        var vResponse = await PersistDerivedAssetAsync(
            aAssetStorage, aAssetRepository, vUserId, vBytes, "image", $"image-op:export:{aRequest.Preset}",
            vSource.Asset.ProjectId, vWidth, vHeight, aCancellationToken);
        return Results.Ok(vResponse);
    }

    // ---- Background removal / transparency (REQ-FN-024) -----------------------------------

    private static async Task<IResult> RemoveBackgroundAsync(
        ImageBackgroundRemovalRequest aRequest,
        ClaimsPrincipal aUser,
        IAssetStorage aAssetStorage,
        IAssetRepository aAssetRepository,
        IImageProcessor aImageProcessor,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vSource = await LoadOwnedSourceAsync(aAssetRepository, aRequest.SourceAssetId, vUserId, aCancellationToken);
        if (vSource is null)
        {
            return Results.NotFound(new { error = "Source asset not found." });
        }

        var vBytes = await aImageProcessor.RemoveBackgroundAsync(vSource.Bytes, aRequest.Tolerance, aCancellationToken);
        var (vWidth, vHeight) = aImageProcessor.Probe(vBytes);
        var vResponse = await PersistDerivedAssetAsync(
            aAssetStorage, aAssetRepository, vUserId, vBytes, "image", "image-op:background-remove",
            vSource.Asset.ProjectId, vWidth, vHeight, aCancellationToken);
        return Results.Ok(vResponse);
    }

    // ---- Shared helpers -------------------------------------------------------------------

    private static async Task<OwnedSource?> LoadOwnedSourceAsync(
        IAssetRepository aAssetRepository, Guid aAssetId, string aUserId, CancellationToken aCancellationToken)
    {
        // Owner-filtered lookup (BRD-81): a user can only operate on their own assets.
        var vAsset = await aAssetRepository.GetAsync(aAssetId, aUserId, aCancellationToken);
        if (vAsset is null || !File.Exists(vAsset.StoragePath))
        {
            return null;
        }

        var vBytes = await File.ReadAllBytesAsync(vAsset.StoragePath, aCancellationToken);
        return new OwnedSource(vAsset, vBytes);
    }

    private static async Task<AssetResponse> PersistDerivedAssetAsync(
        IAssetStorage aAssetStorage,
        IAssetRepository aAssetRepository,
        string aUserId,
        byte[] aBytes,
        string aKind,
        string aModelId,
        Guid? aProjectId,
        int? aWidth,
        int? aHeight,
        CancellationToken aCancellationToken)
    {
        var vOperationId = Guid.NewGuid();
        var vUserFolder = SanitizeForPath(aUserId);
        var vStoragePath = await aAssetStorage.SaveAsync(Path.Combine(vUserFolder, $"{vOperationId}.png"), aBytes, aCancellationToken);

        var vAsset = new GeneratedAsset
        {
            AssetId = Guid.NewGuid(),
            JobId = vOperationId,
            UserId = aUserId,
            ProjectId = aProjectId,
            Kind = aKind,
            StoragePath = vStoragePath,
            ThumbnailPath = vStoragePath,
            Width = aWidth,
            Height = aHeight,
            SeedUsed = null,
            ParamsJson = "{}",
            ModelId = aModelId,
            ProvenanceJson = JsonSerializer.Serialize(new
            {
                Generator = "TrStudio ImageProcessor",
                Operation = aModelId,
                CreatedUtc = DateTime.UtcNow
            }),
            CreatedUtc = DateTime.UtcNow
        };

        await aAssetRepository.InsertAsync(vAsset, aCancellationToken);
        return new AssetResponse
        {
            AssetId = vAsset.AssetId,
            JobId = vAsset.JobId,
            Kind = vAsset.Kind,
            StoragePath = vAsset.StoragePath,
            ThumbnailPath = vAsset.ThumbnailPath,
            Width = vAsset.Width,
            Height = vAsset.Height,
            ModelId = vAsset.ModelId,
            ProjectId = vAsset.ProjectId,
            SeedUsed = vAsset.SeedUsed,
            CreatedUtc = vAsset.CreatedUtc
        };
    }

    private static string SanitizeForPath(string aUserId)
    {
        var vInvalid = Path.GetInvalidFileNameChars();
        var vClean = new string(aUserId.Select(aChar => vInvalid.Contains(aChar) ? '_' : aChar).ToArray());
        return string.IsNullOrWhiteSpace(vClean) ? "unknown" : vClean;
    }

    private static string? ResolveUserId(ClaimsPrincipal aUser)
        => aUser.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? aUser.FindFirst("sub")?.Value;

    private sealed record OwnedSource(GeneratedAsset Asset, byte[] Bytes);
}
