using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Jobs;
using TrStudio.Engine.Domain.Twin;
using TrStudio.Engine.Domain.Video;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Domain.Sequence;

/// <summary>
/// Makes the sequences in the background inside the app (REQ-FN-042): queues one shot through the same
/// job orchestrator the studios use (so the monthly cap, Venice's quote and the polling apply), waits for
/// it, reads its last frame with <see cref="IClipJoiner"/> and stores it as the user's picture, queues
/// the next shot on that picture, and when every shot succeeded joins them into one MP4. A failed shot
/// stops the sequence and the finished shots stay stored.
/// </summary>
public sealed class ClipSequenceRunner : IHostedService, IAsyncDisposable
{
    private readonly IServiceProvider objServices;
    private readonly IClipSequenceRepository objRepository;
    private readonly IJobOrchestrator objOrchestrator;
    private readonly IAssetRepository objAssets;
    private readonly IAssetStorage objStorage;
    private readonly ILogger<ClipSequenceRunner> objLogger;
    private readonly CancellationTokenSource objStopping = new();
    private readonly ConcurrentDictionary<Guid, Task> objRunning = new();
    private int objDisposed;

    /// <summary>Initialises the runner.</summary>
    /// <param name="aServices">Service provider (resolves the service, the joiner and the twin seam when needed).</param>
    /// <param name="aRepository">Stored sequences.</param>
    /// <param name="aOrchestrator">The job pipeline.</param>
    /// <param name="aAssets">Assets.</param>
    /// <param name="aStorage">The local object store.</param>
    /// <param name="aLogger">Logger.</param>
    public ClipSequenceRunner(
        IServiceProvider aServices,
        IClipSequenceRepository aRepository,
        IJobOrchestrator aOrchestrator,
        IAssetRepository aAssets,
        IAssetStorage aStorage,
        ILogger<ClipSequenceRunner> aLogger)
    {
        objServices = aServices;
        objRepository = aRepository;
        objOrchestrator = aOrchestrator;
        objAssets = aAssets;
        objStorage = aStorage;
        objLogger = aLogger;
    }

    /// <summary>How often a shot's job is looked at while it runs.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken aCancellationToken)
        => await objRepository.FailUnfinishedAsync("Stopped when the app was closed; the finished shots are kept.", aCancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken aCancellationToken)
    {
        if (Volatile.Read(ref objDisposed) == 1)
        {
            return;
        }

        await objStopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await Task.WhenAll(objRunning.Values).WaitAsync(TimeSpan.FromSeconds(5), aCancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Closing: a run still going is stopped with the process.
        }
    }

    /// <summary>Starts making a stored sequence in the background.</summary>
    /// <param name="aSequenceId">The sequence.</param>
    /// <param name="aUserId">The owner.</param>
    public void Enqueue(Guid aSequenceId, string aUserId)
    {
        objRunning[aSequenceId] = Task.Run(async () =>
        {
            try
            {
                await RunAsync(aSequenceId, aUserId, objStopping.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Closing.
            }
            catch (Exception vException)
            {
                objLogger.LogError(vException, "Sequence {SequenceId} stopped unexpectedly", aSequenceId);
                await EndAsync(aSequenceId, aUserId, SequenceStates.Failed, "The sequence stopped unexpectedly: " + vException.Message, null).ConfigureAwait(false);
            }
            finally
            {
                objRunning.TryRemove(aSequenceId, out _);
            }
        });
    }

    /// <summary>Waits until every running sequence has ended (tests and shutdown).</summary>
    /// <returns>A task.</returns>
    public Task IdleAsync() => Task.WhenAll(objRunning.Values);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        if (Interlocked.Exchange(ref objDisposed, 1) == 0)
        {
            objStopping.Dispose();
        }
    }

    private async Task RunAsync(Guid aSequenceId, string aUserId, CancellationToken aCancellationToken)
    {
        var vSequence = await objRepository.GetAsync(aSequenceId, aUserId, aCancellationToken).ConfigureAwait(false);
        if (vSequence is null)
        {
            return;
        }

        var vService = objServices.GetRequiredService<ClipSequenceService>();
        var vJoiner = objServices.GetService<IClipJoiner>();
        if (vJoiner is null || !vJoiner.IsAvailable)
        {
            await EndAsync(aSequenceId, aUserId, SequenceStates.Failed, "This computer cannot join clips yet, so nothing was started.", null).ConfigureAwait(false);
            return;
        }

        IDigitalTwinShots? vTwinShots = null;
        Guid? vFirstPicture = null;
        IReadOnlyList<Guid> vPictures = Array.Empty<Guid>();
        if (vSequence.SubjectKind == SequenceSubject.Twin)
        {
            vTwinShots = objServices.GetService<IDigitalTwinShots>();
            var vRefusal = vTwinShots is null ? "Digital twins are not available in this build." : await vTwinShots.RefusalAsync(vSequence.SubjectId, aUserId, aCancellationToken).ConfigureAwait(false);
            if (vRefusal is not null)
            {
                await EndAsync(aSequenceId, aUserId, SequenceStates.Failed, vRefusal, null).ConfigureAwait(false);
                return;
            }
        }
        else
        {
            (vFirstPicture, vPictures) = await vService.CharacterPicturesAsync(vSequence.SubjectId, aUserId, aCancellationToken).ConfigureAwait(false);
        }

        var vSubject = new SequenceSubjectInfo(vSequence.SubjectKind, vSequence.SubjectId, vFirstPicture);
        var vResults = new List<GeneratedAsset>();
        Guid? vFrameAssetId = null;

        foreach (var vShot in vSequence.Shots.OrderBy(aShot => aShot.ShotIndex))
        {
            var vIndex = vShot.ShotIndex - 1;
            vShot.State = SequenceStates.Running;
            vShot.StartFrameAssetId = vFrameAssetId;
            await objRepository.UpdateShotAsync(vShot, aCancellationToken).ConfigureAwait(false);

            try
            {
                vShot.JobId = vSubject.Kind == SequenceSubject.Twin
                    ? await vTwinShots!.QueueClipAsync(vSubject.Id, aUserId, vShot.Prompt, vFrameAssetId, vSequence.Duration, vSequence.Resolution, aCancellationToken).ConfigureAwait(false)
                    : await QueueCharacterShotAsync(vSequence, vSubject, vShot, vIndex, vFirstPicture, vPictures, vFrameAssetId, aCancellationToken).ConfigureAwait(false);
                await objRepository.UpdateShotAsync(vShot, aCancellationToken).ConfigureAwait(false);

                var vStatus = await WaitForJobAsync(vShot.JobId.Value, aUserId, aCancellationToken).ConfigureAwait(false);
                if (vStatus.State != JobState.Succeeded)
                {
                    throw new InvalidOperationException(vStatus.FailureMessage ?? $"the job ended {vStatus.State}");
                }

                var vVideo = (await objAssets.ListByJobAsync(vShot.JobId.Value, aUserId, aCancellationToken).ConfigureAwait(false))
                    .FirstOrDefault(aAsset => aAsset.Kind == "video")
                    ?? throw new InvalidOperationException("the clip finished but no video was stored");
                vShot.ResultAssetId = vVideo.AssetId;
                vResults.Add(vVideo);

                if (vShot.ShotIndex < vSequence.Shots.Count)
                {
                    vFrameAssetId = await StoreLastFrameAsync(vJoiner, vVideo, aUserId, aCancellationToken).ConfigureAwait(false);
                }

                vShot.State = SequenceStates.Succeeded;
                await objRepository.UpdateShotAsync(vShot, aCancellationToken).ConfigureAwait(false);
            }
            catch (Exception vException) when (vException is not OperationCanceledException)
            {
                vShot.State = SequenceStates.Failed;
                await objRepository.UpdateShotAsync(vShot, CancellationToken.None).ConfigureAwait(false);
                await EndAsync(aSequenceId, aUserId, SequenceStates.Failed, $"Shot {vShot.ShotIndex} failed: {vException.Message}", null).ConfigureAwait(false);
                return;
            }
        }

        vSequence.State = SequenceStates.Joining;
        await objRepository.UpdateSequenceAsync(vSequence, aCancellationToken).ConfigureAwait(false);
        try
        {
            var vJoined = await JoinAsync(vJoiner, vSequence, vResults, aCancellationToken).ConfigureAwait(false);
            await EndAsync(aSequenceId, aUserId, SequenceStates.Succeeded, null, vJoined).ConfigureAwait(false);
        }
        catch (Exception vException) when (vException is not OperationCanceledException)
        {
            await EndAsync(aSequenceId, aUserId, SequenceStates.Failed, "The shots are made and kept, but joining them failed: " + vException.Message, null).ConfigureAwait(false);
        }
    }

    private async Task<Guid> QueueCharacterShotAsync(ClipSequence aSequence, SequenceSubjectInfo aSubject, ClipSequenceShot aShot, int aIndex, Guid? aFirstPicture, IReadOnlyList<Guid> aPictures, Guid? aFrameAssetId, CancellationToken aCancellationToken)
    {
        var (vModelId, vTask) = ClipSequenceService.ModelFor(aSubject, aIndex);
        var vParams = new Dictionary<string, object?>
        {
            ["prompt"] = aShot.Prompt,
            ["duration"] = aSequence.Duration,
            ["resolution"] = aSequence.Resolution,
            ["characterId"] = aSubject.Id
        };
        if (vTask == GenerationTaskType.ImageToVideo)
        {
            vParams["sourceAssetId"] = aIndex == 0 ? aFirstPicture : aFrameAssetId;
        }
        else if (aPictures.Count > 0)
        {
            vParams["referenceImageAssetIds"] = aPictures.Take(10).ToList();
        }

        var vResponse = await objOrchestrator.SubmitAsync(new JobSubmitRequest
        {
            ModelId = vModelId,
            Task = vTask,
            Tier = GenerationTier.Premium,
            ParamsJson = JsonSerializer.Serialize(vParams)
        }, aSequence.UserId, aCancellationToken).ConfigureAwait(false);
        return vResponse.JobId;
    }

    private async Task<JobStatusResponse> WaitForJobAsync(Guid aJobId, string aUserId, CancellationToken aCancellationToken)
    {
        while (true)
        {
            var vStatus = await objOrchestrator.GetStatusAsync(aJobId, aUserId, aCancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("the job could not be found");
            if (vStatus.State is JobState.Succeeded or JobState.Failed or JobState.Blocked or JobState.Cancelled)
            {
                return vStatus;
            }

            await Task.Delay(PollInterval, aCancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Guid> StoreLastFrameAsync(IClipJoiner aJoiner, GeneratedAsset aVideo, string aUserId, CancellationToken aCancellationToken)
    {
        var vBytes = await aJoiner.LastFrameAsync(aVideo.StoragePath, aCancellationToken).ConfigureAwait(false);
        var vAssetId = Guid.NewGuid();
        var vPath = await objStorage.SaveAsync(Path.Combine(SafeFolder(aUserId), "sequences", $"{vAssetId:N}.png"), vBytes, aCancellationToken).ConfigureAwait(false);
        await objAssets.InsertAsync(new GeneratedAsset
        {
            AssetId = vAssetId,
            JobId = aVideo.JobId,
            UserId = aUserId,
            ProjectId = aVideo.ProjectId,
            Kind = "image",
            StoragePath = vPath,
            ThumbnailPath = vPath,
            ParamsJson = "{}",
            ModelId = aVideo.ModelId,
            ProvenanceJson = JsonSerializer.Serialize(new { Generator = "TrStudio multi-shot", Source = "last frame", FromAsset = aVideo.AssetId, CreatedUtc = DateTime.UtcNow }),
            CreatedUtc = DateTime.UtcNow
        }, aCancellationToken).ConfigureAwait(false);
        return vAssetId;
    }

    private async Task<Guid> JoinAsync(IClipJoiner aJoiner, ClipSequence aSequence, IReadOnlyList<GeneratedAsset> aResults, CancellationToken aCancellationToken)
    {
        var vTemp = Path.Combine(Path.GetTempPath(), "trstudio-sequence", $"{aSequence.SequenceId:N}.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(vTemp)!);
        try
        {
            var vOutcome = await aJoiner.JoinAsync(aResults.Select(aAsset => aAsset.StoragePath).ToList(), vTemp, aCancellationToken).ConfigureAwait(false);
            if (!vOutcome.Ok || !File.Exists(vTemp))
            {
                throw new InvalidOperationException(vOutcome.Problem ?? $"{aJoiner.Name} wrote no file");
            }

            var vBytes = await File.ReadAllBytesAsync(vTemp, aCancellationToken).ConfigureAwait(false);
            var vLast = aResults[^1];
            var vAssetId = Guid.NewGuid();
            var vPath = await objStorage.SaveAsync(Path.Combine(SafeFolder(aSequence.UserId), "sequences", $"{aSequence.SequenceId:N}.mp4"), vBytes, aCancellationToken).ConfigureAwait(false);
            await objAssets.InsertAsync(new GeneratedAsset
            {
                AssetId = vAssetId,
                JobId = vLast.JobId,
                UserId = aSequence.UserId,
                ProjectId = vLast.ProjectId,
                Kind = "video",
                StoragePath = vPath,
                Width = vOutcome.Width > 0 ? vOutcome.Width : null,
                Height = vOutcome.Height > 0 ? vOutcome.Height : null,
                ParamsJson = JsonSerializer.Serialize(new { sequenceId = aSequence.SequenceId, subjectKind = aSequence.SubjectKind, subjectId = aSequence.SubjectId, shots = aResults.Count }),
                ModelId = vLast.ModelId,
                ProvenanceJson = JsonSerializer.Serialize(new { Generator = "TrStudio multi-shot", Joiner = aJoiner.Name, Shots = aResults.Select(aAsset => aAsset.AssetId), CreatedUtc = DateTime.UtcNow }),
                CreatedUtc = DateTime.UtcNow
            }, aCancellationToken).ConfigureAwait(false);
            return vAssetId;
        }
        finally
        {
            try
            {
                File.Delete(vTemp);
            }
            catch (IOException)
            {
                // A temporary file left behind is harmless.
            }
        }
    }

    private async Task EndAsync(Guid aSequenceId, string aUserId, string aState, string? aMessage, Guid? aJoinedAssetId)
    {
        var vSequence = await objRepository.GetAsync(aSequenceId, aUserId).ConfigureAwait(false);
        if (vSequence is null)
        {
            return;
        }

        vSequence.State = aState;
        vSequence.Message = aMessage;
        vSequence.JoinedAssetId = aJoinedAssetId;
        vSequence.FinishedUtc = DateTime.UtcNow;
        await objRepository.UpdateSequenceAsync(vSequence).ConfigureAwait(false);
    }

    private static string SafeFolder(string aUserId)
    {
        var vInvalid = Path.GetInvalidFileNameChars();
        var vClean = new string(aUserId.Select(aChar => vInvalid.Contains(aChar) ? '_' : aChar).ToArray());
        return string.IsNullOrWhiteSpace(vClean) ? "unknown" : vClean;
    }
}
