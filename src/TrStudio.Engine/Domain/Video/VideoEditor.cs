using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Domain.Video;

/// <summary>
/// Default <see cref="IVideoEditor"/> (FN-054). Validates every owner-scoped asset (primary clip,
/// joined clips, added audio — BRD-81), then builds and runs an ffmpeg pipeline that trims, joins,
/// reframes (scale + letterbox pad to the chosen platform aspect) and muxes audio, exporting an MP4
/// (H.264/AAC). When ffmpeg is NOT installed the whole step degrades cleanly to a Degraded outcome
/// carrying an actionable "ffmpeg not installed" message instead of crashing. A real export persists
/// a new owner-scoped video asset and returns its id + dimensions.
/// </summary>
public class VideoEditor : IVideoEditor
{
    private const string FfmpegNotInstalledMessage =
        "ffmpeg is not installed on the host — install ffmpeg (e.g. 'apt install ffmpeg' / 'brew install ffmpeg') " +
        "to enable video trim/join/reframe/add-audio and MP4 export.";

    private readonly IAssetRepository objAssetRepository;
    private readonly IAssetStorage objAssetStorage;
    private readonly IFfmpegRunner objFfmpeg;
    private readonly ILogger<VideoEditor> objLogger;

    /// <summary>
    /// Initialises the editor.
    /// </summary>
    /// <param name="aAssetRepository">Owner-scoped asset lookup + output persistence (BRD-81).</param>
    /// <param name="aAssetStorage">Local object store receiving the exported MP4.</param>
    /// <param name="aFfmpeg">The ffmpeg CLI seam (availability + invocation).</param>
    /// <param name="aLogger">Structured logger.</param>
    public VideoEditor(
        IAssetRepository aAssetRepository,
        IAssetStorage aAssetStorage,
        IFfmpegRunner aFfmpeg,
        ILogger<VideoEditor> aLogger)
    {
        objAssetRepository = aAssetRepository;
        objAssetStorage = aAssetStorage;
        objFfmpeg = aFfmpeg;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<VideoEditResponse?> EditAsync(VideoEditRequest aRequest, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vFormat = VideoFormat.Normalize(aRequest.ExportFormat);
        if (vFormat is null)
        {
            return new VideoEditResponse
            {
                Status = "error",
                ExportFormat = aRequest.ExportFormat,
                FfmpegAvailable = objFfmpeg.IsAvailable,
                Message = $"ExportFormat must be one of: {string.Join(", ", VideoFormat.Supported)}."
            };
        }

        // ---- Owner-scoped primary clip (BRD-81): null → 404 (not found/owned). ----
        var vPrimary = await objAssetRepository.GetAsync(aRequest.SourceAssetId, aUserId, aCancellationToken).ConfigureAwait(false);
        if (vPrimary is null)
        {
            objLogger.LogWarning("Video edit rejected: primary asset {AssetId} not found for user {UserId}", aRequest.SourceAssetId, aUserId);
            return null;
        }

        var vResponse = new VideoEditResponse { ExportFormat = vFormat, FfmpegAvailable = objFfmpeg.IsAvailable };
        var vOperations = new List<string>();

        // ---- Owner-scoped joined clips + added audio. ----
        var vJoinPaths = new List<string>();
        foreach (var vJoinId in aRequest.JoinAssetIds.Where(aId => aId != Guid.Empty))
        {
            var vJoin = await objAssetRepository.GetAsync(vJoinId, aUserId, aCancellationToken).ConfigureAwait(false);
            if (vJoin is null)
            {
                vResponse.Status = "error";
                vResponse.Message = $"The join asset '{vJoinId}' was not found or is not owned by the caller.";
                return vResponse;
            }

            vJoinPaths.Add(vJoin.StoragePath);
        }

        string? vAudioPath = null;
        if (aRequest.AudioAssetId is { } vAudioId && vAudioId != Guid.Empty)
        {
            var vAudio = await objAssetRepository.GetAsync(vAudioId, aUserId, aCancellationToken).ConfigureAwait(false);
            if (vAudio is null)
            {
                vResponse.Status = "error";
                vResponse.Message = $"The audio asset '{vAudioId}' was not found or is not owned by the caller.";
                return vResponse;
            }

            vAudioPath = vAudio.StoragePath;
        }

        // Describe the requested operations (surfaced whether the edit runs for real or degrades).
        if (aRequest.TrimStartSeconds is not null || aRequest.TrimEndSeconds is not null)
        {
            vOperations.Add($"trim[{aRequest.TrimStartSeconds ?? 0:0.###}s..{(aRequest.TrimEndSeconds?.ToString("0.###", CultureInfo.InvariantCulture) ?? "end")}]");
        }

        if (vJoinPaths.Count > 0)
        {
            vOperations.Add($"join[+{vJoinPaths.Count}]");
        }

        var (vWidth, vHeight) = VideoFormat.Dimensions(vFormat);
        vOperations.Add($"reframe[{vFormat} {vWidth}x{vHeight}]");

        if (vAudioPath is not null)
        {
            vOperations.Add("add-audio");
        }

        vOperations.Add("export[mp4]");
        vResponse.Operations = vOperations;

        // ---- Graceful degrade: ffmpeg absent → clean, actionable message (no crash). ----
        if (!objFfmpeg.IsAvailable)
        {
            objLogger.LogInformation("Video edit degraded for user {UserId}: ffmpeg not installed", aUserId);
            vResponse.Status = "degraded";
            vResponse.Degraded = true;
            vResponse.Message = FfmpegNotInstalledMessage;
            return vResponse;
        }

        return await RunRealEditAsync(aRequest, aUserId, vPrimary, vJoinPaths, vAudioPath, vFormat, vWidth, vHeight, vResponse, aCancellationToken).ConfigureAwait(false);
    }

    private async Task<VideoEditResponse> RunRealEditAsync(
        VideoEditRequest aRequest,
        string aUserId,
        GeneratedAsset aPrimary,
        IReadOnlyList<string> aJoinPaths,
        string? aAudioPath,
        string aFormat,
        int aWidth,
        int aHeight,
        VideoEditResponse aResponse,
        CancellationToken aCancellationToken)
    {
        if (!File.Exists(aPrimary.StoragePath))
        {
            aResponse.Status = "error";
            aResponse.Message = "The primary source clip binary is missing from storage.";
            return aResponse;
        }

        var vTempOutput = Path.Combine(Path.GetTempPath(), $"trstudio-edit-{Guid.NewGuid():N}.mp4");
        try
        {
            var vArgs = BuildFfmpegArgs(aRequest, aPrimary.StoragePath, aJoinPaths, aAudioPath, aWidth, aHeight, vTempOutput);
            var vRun = await objFfmpeg.RunAsync(vArgs, aCancellationToken).ConfigureAwait(false);
            if (!vRun.Succeeded || !File.Exists(vTempOutput))
            {
                objLogger.LogWarning("ffmpeg edit failed (exit {ExitCode}) for user {UserId}", vRun.ExitCode, aUserId);
                aResponse.Status = "error";
                aResponse.Message = "The ffmpeg edit failed; see the server log for the ffmpeg diagnostics.";
                return aResponse;
            }

            var vBytes = await File.ReadAllBytesAsync(vTempOutput, aCancellationToken).ConfigureAwait(false);
            var vAssetId = Guid.NewGuid();
            var vUserFolder = SanitizeForPath(aUserId);
            var vStoragePath = await objAssetStorage
                .SaveAsync(Path.Combine(vUserFolder, "video", $"{vAssetId}.mp4"), vBytes, aCancellationToken)
                .ConfigureAwait(false);

            var vAsset = new GeneratedAsset
            {
                AssetId = vAssetId,
                JobId = Guid.Empty, // Edit exports are synchronous, not a queued generation job.
                UserId = aUserId,
                ProjectId = aRequest.ProjectId ?? aPrimary.ProjectId,
                Kind = "video",
                StoragePath = vStoragePath,
                ThumbnailPath = null,
                Width = aWidth,
                Height = aHeight,
                SeedUsed = null,
                ParamsJson = JsonSerializer.Serialize(new
                {
                    Source = aPrimary.AssetId,
                    Operations = aResponse.Operations,
                    ExportFormat = aFormat
                }),
                ModelId = "video-editor-ffmpeg",
                ProvenanceJson = JsonSerializer.Serialize(new
                {
                    Generator = "TrStudio VideoEditor (ffmpeg)",
                    Source = aPrimary.AssetId,
                    ExportFormat = aFormat,
                    CreatedUtc = DateTime.UtcNow
                }),
                CreatedUtc = DateTime.UtcNow
            };

            await objAssetRepository.InsertAsync(vAsset, aCancellationToken).ConfigureAwait(false);

            aResponse.Status = "ok";
            aResponse.Width = aWidth;
            aResponse.Height = aHeight;
            aResponse.OutputAssetId = vAssetId;
            aResponse.Message = $"Exported an MP4 at {aFormat} ({aWidth}x{aHeight}).";
            return aResponse;
        }
        finally
        {
            TryDelete(vTempOutput);
        }
    }

    /// <summary>
    /// Builds the ffmpeg argument string for the requested edit: input seeking for trim, a
    /// scale+pad reframe to the target platform dimensions, N-way concat for join, and an optional
    /// audio mux. H.264 video + AAC audio in an MP4 container.
    /// </summary>
    private static string BuildFfmpegArgs(
        VideoEditRequest aRequest,
        string aPrimaryPath,
        IReadOnlyList<string> aJoinPaths,
        string? aAudioPath,
        int aWidth,
        int aHeight,
        string aOutputPath)
    {
        var vArgs = new StringBuilder("-y ");

        // Trim applies to the primary clip via input seeking.
        if (aRequest.TrimStartSeconds is { } vStart && vStart > 0)
        {
            vArgs.Append(CultureInfo.InvariantCulture, $"-ss {vStart:0.###} ");
        }

        if (aRequest.TrimEndSeconds is { } vEnd && vEnd > 0)
        {
            vArgs.Append(CultureInfo.InvariantCulture, $"-to {vEnd:0.###} ");
        }

        vArgs.Append(CultureInfo.InvariantCulture, $"-i \"{aPrimaryPath}\" ");
        foreach (var vJoin in aJoinPaths)
        {
            vArgs.Append(CultureInfo.InvariantCulture, $"-i \"{vJoin}\" ");
        }

        var vAudioInputIndex = 1 + aJoinPaths.Count;
        if (aAudioPath is not null)
        {
            vArgs.Append(CultureInfo.InvariantCulture, $"-i \"{aAudioPath}\" ");
        }

        var vPad = $"scale={aWidth}:{aHeight}:force_original_aspect_ratio=decrease," +
                   $"pad={aWidth}:{aHeight}:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1";
        var vVideoCount = 1 + aJoinPaths.Count;

        if (vVideoCount == 1)
        {
            vArgs.Append(CultureInfo.InvariantCulture, $"-vf \"{vPad}\" ");
            AppendAudioMap(vArgs, aAudioPath, vAudioInputIndex, singleInput: true);
        }
        else
        {
            var vFilter = new StringBuilder();
            for (var vIndex = 0; vIndex < vVideoCount; vIndex++)
            {
                vFilter.Append(CultureInfo.InvariantCulture, $"[{vIndex}:v]{vPad}[v{vIndex}];");
            }

            for (var vIndex = 0; vIndex < vVideoCount; vIndex++)
            {
                vFilter.Append(CultureInfo.InvariantCulture, $"[v{vIndex}]");
            }

            vFilter.Append(CultureInfo.InvariantCulture, $"concat=n={vVideoCount}:v=1:a=0[outv]");
            vArgs.Append(CultureInfo.InvariantCulture, $"-filter_complex \"{vFilter}\" -map \"[outv]\" ");
            AppendAudioMap(vArgs, aAudioPath, vAudioInputIndex, singleInput: false);
        }

        vArgs.Append(CultureInfo.InvariantCulture, $"-c:v libx264 -preset veryfast -pix_fmt yuv420p -movflags +faststart \"{aOutputPath}\"");
        return vArgs.ToString();
    }

    private static void AppendAudioMap(StringBuilder aArgs, string? aAudioPath, int aAudioInputIndex, bool singleInput)
    {
        // The concat/join path already maps the composed video via -map "[outv]"; the single-input
        // path maps the -vf-filtered primary video explicitly so a second (audio) input is unambiguous.
        if (aAudioPath is not null)
        {
            // Mux the added Voice-studio audio track (FN-054 add-audio), trimmed to the video length.
            if (singleInput)
            {
                aArgs.Append("-map 0:v:0 ");
            }

            aArgs.Append(CultureInfo.InvariantCulture, $"-map {aAudioInputIndex}:a:0 -c:a aac -shortest ");
        }
        else if (singleInput)
        {
            // Keep the primary clip's own audio when present (optional map so it never fails).
            aArgs.Append("-map 0:v:0 -map 0:a:0? -c:a aac ");
        }
        else
        {
            // Joined clips export video-only unless an audio track is added.
            aArgs.Append("-an ");
        }
    }

    private static void TryDelete(string aPath)
    {
        try
        {
            if (File.Exists(aPath))
            {
                File.Delete(aPath);
            }
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    private static string SanitizeForPath(string aUserId)
    {
        var vInvalid = Path.GetInvalidFileNameChars();
        var vClean = new string(aUserId.Select(aChar => vInvalid.Contains(aChar) ? '_' : aChar).ToArray());
        return string.IsNullOrWhiteSpace(vClean) ? "unknown" : vClean;
    }
}
