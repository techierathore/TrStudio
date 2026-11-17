using AVFoundation;
using CoreGraphics;
using CoreMedia;
using Foundation;
using TrStudio.Engine.Domain.Video;
using UIKit;

namespace TrStudio.Platforms.MacCatalyst;

/// <summary>
/// The Mac app's <see cref="IClipJoiner"/> (REQ-FN-055, REQ-NFR-023): joins clips with Apple's
/// AVFoundation, which is built into macOS and allowed inside the app sandbox, so nothing is
/// downloaded or sent and no ffmpeg is needed. The clips are laid end to end in an
/// <see cref="AVMutableComposition"/>, drawn at the first clip's size, and exported to an MP4.
/// </summary>
public sealed class AvFoundationClipJoiner : IClipJoiner
{
    /// <inheritdoc />
    public string Name => "AVFoundation";

    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public Task<ClipJoinResult> JoinAsync(IReadOnlyList<string> aInputPaths, string aOutputPath, CancellationToken aCancellationToken = default)
    {
        if (aInputPaths.Count < 2)
        {
            return Task.FromResult(new ClipJoinResult(false, 0, 0, 0, "Joining needs at least two clips."));
        }

        return Task.Run(() => JoinCore(aInputPaths, aOutputPath, aCancellationToken), aCancellationToken);
    }

    /// <inheritdoc />
    public Task<byte[]> LastFrameAsync(string aVideoPath, CancellationToken aCancellationToken = default)
        => Task.Run(() =>
        {
            using var vAsset = AVAsset.FromUrl(NSUrl.FromFilename(aVideoPath));
            var vSeconds = Math.Max(0, vAsset.Duration.Seconds - 0.1);
            using var vGenerator = new AVAssetImageGenerator(vAsset) { AppliesPreferredTrackTransform = true };
            var vTime = CMTime.FromSeconds(vSeconds, 600);
            using var vImage = vGenerator.CopyCGImageAtTime(vTime, out _, out var vError);
            if (vImage is null)
            {
                throw new InvalidOperationException("The last picture of the clip could not be read: " + vError?.LocalizedDescription);
            }

            using var vUiImage = UIImage.FromImage(vImage);
            using var vPng = vUiImage.AsPNG();
            return vPng?.ToArray() ?? throw new InvalidOperationException("The last picture of the clip could not be encoded.");
        }, aCancellationToken);

    /// <inheritdoc />
    public Task<double> DurationSecondsAsync(string aVideoPath, CancellationToken aCancellationToken = default)
        => Task.Run(() =>
        {
            using var vAsset = AVAsset.FromUrl(NSUrl.FromFilename(aVideoPath));
            var vSeconds = vAsset.Duration.Seconds;
            if (double.IsNaN(vSeconds) || vSeconds <= 0)
            {
                throw new InvalidOperationException("The clip's length could not be read.");
            }

            return vSeconds;
        }, aCancellationToken);

    private static ClipJoinResult JoinCore(IReadOnlyList<string> aInputPaths, string aOutputPath, CancellationToken aCancellationToken)
    {
        var vAssets = aInputPaths.Select(aPath => AVAsset.FromUrl(NSUrl.FromFilename(aPath))).ToList();
        var vVideoSources = new List<AVAssetTrack>();
        foreach (var vAsset in vAssets)
        {
            var vTrack = vAsset.TracksWithMediaType(AVMediaTypes.Video.GetConstant()).FirstOrDefault();
            if (vTrack is null)
            {
                return new ClipJoinResult(false, 0, 0, 0, "A clip has no picture track.");
            }

            vVideoSources.Add(vTrack);
        }

        var vWithAudio = vAssets.All(aAsset => aAsset.TracksWithMediaType(AVMediaTypes.Audio.GetConstant()).Length > 0);

        // The first clip sets the frame size (its picture turned the way it is meant to be seen).
        var vFirstSize = DisplaySize(vVideoSources[0]);
        var vWidth = Math.Max(2, (int)vFirstSize.Width / 2 * 2);
        var vHeight = Math.Max(2, (int)vFirstSize.Height / 2 * 2);
        var vRender = new CGSize(vWidth, vHeight);

        var vComposition = new AVMutableComposition();
        var vVideoTrack = vComposition.AddMutableTrack(AVMediaTypes.Video.GetConstant(), 0);
        var vAudioTrack = vWithAudio ? vComposition.AddMutableTrack(AVMediaTypes.Audio.GetConstant(), 0) : null;
        var vInstructions = new List<AVVideoCompositionInstruction>();
        var vCursor = CMTime.Zero;

        for (var vIndex = 0; vIndex < vAssets.Count; vIndex++)
        {
            aCancellationToken.ThrowIfCancellationRequested();
            var vAsset = vAssets[vIndex];
            var vRange = new CMTimeRange { Start = CMTime.Zero, Duration = vAsset.Duration };
            if (!vVideoTrack.InsertTimeRange(vRange, vVideoSources[vIndex], vCursor, out var vError))
            {
                return new ClipJoinResult(false, 0, 0, 0, "A clip could not be added: " + vError?.LocalizedDescription);
            }

            if (vAudioTrack is not null)
            {
                var vAudioSource = vAsset.TracksWithMediaType(AVMediaTypes.Audio.GetConstant()).First();
                vAudioTrack.InsertTimeRange(vRange, vAudioSource, vCursor, out _);
            }

            var vLayer = AVMutableVideoCompositionLayerInstruction.FromAssetTrack(vVideoTrack);
            vLayer.SetTransform(FitTransform(vVideoSources[vIndex], vRender), vCursor);
            var vInstruction = new AVMutableVideoCompositionInstruction
            {
                TimeRange = new CMTimeRange { Start = vCursor, Duration = vAsset.Duration },
                LayerInstructions = new AVVideoCompositionLayerInstruction[] { vLayer },
                BackgroundColor = UIColor.Black.CGColor
            };
            vInstructions.Add(vInstruction);
            vCursor = CMTime.Add(vCursor, vAsset.Duration);
        }

        var vFrameRate = vVideoSources[0].NominalFrameRate;
        var vVideoComposition = new AVMutableVideoComposition
        {
            RenderSize = vRender,
            FrameDuration = CMTime.FromSeconds(1.0 / (vFrameRate > 1 ? vFrameRate : 24), 600),
            Instructions = vInstructions.ToArray()
        };

        if (File.Exists(aOutputPath))
        {
            File.Delete(aOutputPath);
        }

        using var vSession = new AVAssetExportSession(vComposition, AVAssetExportSessionPreset.HighestQuality)
        {
            OutputUrl = NSUrl.FromFilename(aOutputPath),
            OutputFileType = AVFileTypes.Mpeg4.GetConstant(),
            VideoComposition = vVideoComposition,
            ShouldOptimizeForNetworkUse = true
        };

        using var vDone = new ManualResetEventSlim(false);
        vSession.ExportAsynchronously(() => vDone.Set());
        using (aCancellationToken.Register(() => vSession.CancelExport()))
        {
            vDone.Wait();
        }

        if (vSession.Status != AVAssetExportSessionStatus.Completed || !File.Exists(aOutputPath))
        {
            return new ClipJoinResult(false, 0, 0, 0, "The clips could not be joined: " + (vSession.Error?.LocalizedDescription ?? vSession.Status.ToString()));
        }

        return new ClipJoinResult(true, vComposition.Duration.Seconds, vWidth, vHeight, null);
    }

    /// <summary>The picture's size as it is seen (the track's own turn applied).</summary>
    private static CGSize DisplaySize(AVAssetTrack aTrack)
    {
        var vRect = aTrack.PreferredTransform.TransformRect(new CGRect(CGPoint.Empty, aTrack.NaturalSize));
        return new CGSize(Math.Abs(vRect.Width), Math.Abs(vRect.Height));
    }

    /// <summary>The turn and scale that fit a clip inside the frame, centred, black bars around it.</summary>
    private static CGAffineTransform FitTransform(AVAssetTrack aTrack, CGSize aRender)
    {
        var vPreferred = aTrack.PreferredTransform;
        var vRect = vPreferred.TransformRect(new CGRect(CGPoint.Empty, aTrack.NaturalSize));
        var vScale = (nfloat)Math.Min(aRender.Width / vRect.Width, aRender.Height / vRect.Height);
        var vShownWidth = vRect.Width * vScale;
        var vShownHeight = vRect.Height * vScale;

        var vTransform = CGAffineTransform.Multiply(vPreferred, CGAffineTransform.MakeTranslation(-vRect.X, -vRect.Y));
        vTransform = CGAffineTransform.Multiply(vTransform, CGAffineTransform.MakeScale(vScale, vScale));
        return CGAffineTransform.Multiply(vTransform, CGAffineTransform.MakeTranslation((aRender.Width - vShownWidth) / 2, (aRender.Height - vShownHeight) / 2));
    }
}
