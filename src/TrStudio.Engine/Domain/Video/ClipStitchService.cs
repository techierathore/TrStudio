using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Cost;
using TrStudio.Engine.Domain.Jobs;
using TrStudio.Engine.Domain.Safety;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Domain.Video;

/// <summary>A request to join finished clips end to end (REQ-FN-055).</summary>
public sealed class ClipStitchRequest
{
    /// <summary>The user's video assets, in order.</summary>
    public List<Guid> AssetIds { get; set; } = new();

    /// <summary>Finished jobs whose result video is joined (after <see cref="AssetIds"/>, in order).</summary>
    public List<Guid> ClipJobIds { get; set; } = new();

    /// <summary>The project to attach the new clip to, if any.</summary>
    public Guid? ProjectId { get; set; }
}

/// <summary>A request to make a clip longer through Venice (REQ-FN-114).</summary>
public sealed class ClipExtendRequest
{
    /// <summary>The user's video asset to extend.</summary>
    public Guid AssetId { get; set; }

    /// <summary>How many seconds to add.</summary>
    public double Seconds { get; set; }

    /// <summary>What happens next; empty means "continue the action naturally".</summary>
    public string? Prompt { get; set; }
}

/// <summary>The outcome of a join or an extend.</summary>
/// <param name="Kind">"ok", "notFound" or "refused".</param>
/// <param name="Message">The plain reason when not ok.</param>
/// <param name="AssetId">The new clip (join).</param>
/// <param name="JobId">The queued job (extend).</param>
/// <param name="DurationSeconds">The joined clip's length (join).</param>
/// <param name="Width">The joined clip's width (join).</param>
/// <param name="Height">The joined clip's height (join).</param>
/// <param name="JoinedBy">The name of what did the joining (join).</param>
public sealed record ClipStitchOutcome(
    string Kind,
    string? Message = null,
    Guid? AssetId = null,
    Guid? JobId = null,
    double? DurationSeconds = null,
    int? Width = null,
    int? Height = null,
    string? JoinedBy = null);

/// <summary>
/// Makes a clip longer (REQ-FN-055, REQ-FN-114): joins finished clips on this computer with the
/// <see cref="IClipJoiner"/> (no network call), or queues one Venice Wan 3.0 reference-to-video job
/// that continues a clip, through the same job pipeline the studios use so the monthly cap, the
/// Venice quote and the polling apply.
/// </summary>
public sealed class ClipStitchService
{
    /// <summary>The registry row the extend job runs on.</summary>
    public const string WanReferenceModelId = "venice-wan3-r2v";

    /// <summary>The longest clip Wan accepts as a reference, in seconds.</summary>
    public const double MaxReferenceSeconds = 15;

    private static readonly int[] WanDurations = { 2, 5, 10, 15, 20, 25, 30 };

    private readonly IAssetRepository objAssets;
    private readonly IAssetStorage objStorage;
    private readonly IClipJoiner objJoiner;
    private readonly IJobOrchestrator objOrchestrator;
    private readonly ILogger<ClipStitchService> objLogger;

    /// <summary>Initialises the service.</summary>
    /// <param name="aAssets">The owner-scoped asset rows.</param>
    /// <param name="aStorage">The local asset store.</param>
    /// <param name="aJoiner">The joiner (AVFoundation in the Mac app, ffmpeg otherwise).</param>
    /// <param name="aOrchestrator">The job pipeline.</param>
    /// <param name="aLogger">The logger.</param>
    public ClipStitchService(IAssetRepository aAssets, IAssetStorage aStorage, IClipJoiner aJoiner, IJobOrchestrator aOrchestrator, ILogger<ClipStitchService> aLogger)
    {
        objAssets = aAssets;
        objStorage = aStorage;
        objJoiner = aJoiner;
        objOrchestrator = aOrchestrator;
        objLogger = aLogger;
    }

    /// <summary>Joins the clips end to end on this computer and stores one new video asset.</summary>
    /// <param name="aRequest">The clips.</param>
    /// <param name="aUserId">The owner.</param>
    /// <param name="aCancellationToken">Token to cancel the join.</param>
    /// <returns>The outcome.</returns>
    public async Task<ClipStitchOutcome> StitchAsync(ClipStitchRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vAssets = new List<GeneratedAsset>();
        foreach (var vId in aRequest.AssetIds)
        {
            var vAsset = await objAssets.GetAsync(vId, aUserId, aCancellationToken).ConfigureAwait(false);
            if (vAsset is null || !IsVideo(vAsset))
            {
                return new ClipStitchOutcome("notFound", "A clip was not found among your videos.");
            }

            vAssets.Add(vAsset);
        }

        foreach (var vJobId in aRequest.ClipJobIds)
        {
            var vResults = await objAssets.ListByJobAsync(vJobId, aUserId, aCancellationToken).ConfigureAwait(false);
            var vClip = vResults.FirstOrDefault(IsVideo);
            if (vClip is null)
            {
                return new ClipStitchOutcome("notFound", "A clip was not found among your finished jobs.");
            }

            vAssets.Add(vClip);
        }

        if (vAssets.Count < 2)
        {
            return new ClipStitchOutcome("refused", "Pick at least two clips to join.");
        }

        if (!objJoiner.IsAvailable)
        {
            return new ClipStitchOutcome("refused", $"Joining clips is not available on this computer ({objJoiner.Name}).");
        }

        var vMissing = vAssets.FirstOrDefault(aAsset => !File.Exists(aAsset.StoragePath));
        if (vMissing is not null)
        {
            return new ClipStitchOutcome("refused", "A clip's file is missing from this computer.");
        }

        var vTemp = Path.Combine(Path.GetTempPath(), $"trstudio-join-{Guid.NewGuid():N}.mp4");
        try
        {
            var vJoin = await objJoiner.JoinAsync(vAssets.Select(aAsset => aAsset.StoragePath).ToList(), vTemp, aCancellationToken).ConfigureAwait(false);
            if (!vJoin.Ok || !File.Exists(vTemp))
            {
                return new ClipStitchOutcome("refused", vJoin.Problem ?? "The clips could not be joined.");
            }

            var vBytes = await File.ReadAllBytesAsync(vTemp, aCancellationToken).ConfigureAwait(false);
            var vAssetId = Guid.NewGuid();
            var vStoragePath = await objStorage
                .SaveAsync(Path.Combine(SanitizeForPath(aUserId), "video", $"{vAssetId}.mp4"), vBytes, aCancellationToken)
                .ConfigureAwait(false);

            await objAssets.InsertAsync(new GeneratedAsset
            {
                AssetId = vAssetId,
                JobId = Guid.Empty,
                UserId = aUserId,
                ProjectId = aRequest.ProjectId ?? vAssets[0].ProjectId,
                Kind = "video",
                StoragePath = vStoragePath,
                Width = vJoin.Width,
                Height = vJoin.Height,
                ParamsJson = JsonSerializer.Serialize(new
                {
                    Sources = vAssets.Select(aAsset => aAsset.AssetId),
                    durationSeconds = Math.Round(vJoin.DurationSeconds),
                    JoinedBy = objJoiner.Name
                }),
                ModelId = $"clip-join-{objJoiner.Name.ToLowerInvariant()}",
                ProvenanceJson = JsonSerializer.Serialize(new
                {
                    Generator = $"TrStudio clip join ({objJoiner.Name})",
                    Sources = vAssets.Select(aAsset => aAsset.AssetId),
                    CreatedUtc = DateTime.UtcNow
                }),
                CreatedUtc = DateTime.UtcNow
            }, aCancellationToken).ConfigureAwait(false);

            return new ClipStitchOutcome("ok", null, vAssetId, null, vJoin.DurationSeconds, vJoin.Width, vJoin.Height, objJoiner.Name);
        }
        finally
        {
            try
            {
                File.Delete(vTemp);
            }
            catch (IOException)
            {
                // Best-effort temp cleanup.
            }
        }
    }

    /// <summary>Queues one Venice Wan 3.0 reference-to-video job that continues a clip.</summary>
    /// <param name="aRequest">The clip and the seconds to add.</param>
    /// <param name="aUserId">The owner.</param>
    /// <param name="aCancellationToken">Token to cancel the submit.</param>
    /// <returns>The outcome with the job id.</returns>
    public async Task<ClipStitchOutcome> ExtendAsync(ClipExtendRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vAsset = await objAssets.GetAsync(aRequest.AssetId, aUserId, aCancellationToken).ConfigureAwait(false);
        if (vAsset is null || !IsVideo(vAsset))
        {
            return new ClipStitchOutcome("notFound", "The clip was not found among your videos.");
        }

        if (aRequest.Seconds < 1 || aRequest.Seconds > 30)
        {
            return new ClipStitchOutcome("refused", "Choose between 1 and 30 seconds to add.");
        }

        if (!File.Exists(vAsset.StoragePath))
        {
            return new ClipStitchOutcome("refused", "The clip's file is missing from this computer.");
        }

        double vLength;
        try
        {
            vLength = await objJoiner.DurationSecondsAsync(vAsset.StoragePath, aCancellationToken).ConfigureAwait(false);
        }
        catch (Exception vException) when (vException is InvalidOperationException or IOException)
        {
            objLogger.LogWarning(vException, "Could not read the length of clip {AssetId}", vAsset.AssetId);
            return new ClipStitchOutcome("refused", "The clip's length could not be read.");
        }

        if (vLength > MaxReferenceSeconds + 0.25)
        {
            return new ClipStitchOutcome("refused", $"Venice reads clips of up to {MaxReferenceSeconds:0} seconds to extend; this one is {vLength:0.#} seconds. Trim it first.");
        }

        var vWanted = vLength + aRequest.Seconds - 0.25;
        var vDuration = WanDurations.FirstOrDefault(aSeconds => aSeconds >= vWanted);
        if (vDuration == 0)
        {
            return new ClipStitchOutcome("refused", "Wan makes at most 30 seconds in one go; add fewer seconds.");
        }

        var vSecondsText = aRequest.Seconds.ToString("0.#", CultureInfo.InvariantCulture);
        var vWhat = string.IsNullOrWhiteSpace(aRequest.Prompt) ? "continue the action naturally" : aRequest.Prompt.Trim();
        var vParams = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["prompt"] = $"Extend @Video1 by {vSecondsText} seconds: {vWhat}",
            ["referenceVideoAssetIds"] = new[] { vAsset.AssetId.ToString() },
            ["referenceVideoSeconds"] = Math.Round(vLength, 2),
            ["duration"] = $"{vDuration}s",
            ["resolution"] = "480p"
        });

        try
        {
            var vJob = await objOrchestrator.SubmitAsync(new JobSubmitRequest
            {
                ModelId = WanReferenceModelId,
                Task = GenerationTaskType.TextToVideo,
                Tier = GenerationTier.Premium,
                ParamsJson = vParams,
                ProjectId = vAsset.ProjectId
            }, aUserId, aCancellationToken).ConfigureAwait(false);
            return new ClipStitchOutcome("ok", null, null, vJob.JobId);
        }
        catch (Exception vException) when (vException is KeyNotFoundException or ArgumentException or SpendCapExceededException or SafetyBlockedException)
        {
            return new ClipStitchOutcome("refused", vException.Message);
        }
    }

    private static bool IsVideo(GeneratedAsset aAsset) => string.Equals(aAsset.Kind, "video", StringComparison.OrdinalIgnoreCase);

    private static string SanitizeForPath(string aUserId)
    {
        var vInvalid = Path.GetInvalidFileNameChars();
        var vClean = new string(aUserId.Select(aChar => vInvalid.Contains(aChar) ? '_' : aChar).ToArray());
        return string.IsNullOrWhiteSpace(vClean) ? "unknown" : vClean;
    }
}
