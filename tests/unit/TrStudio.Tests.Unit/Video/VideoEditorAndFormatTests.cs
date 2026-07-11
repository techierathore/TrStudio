using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Video;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Video;

/// <summary>
/// Unit tests for the FN-054 video editor's degrade + owner-scope behaviour and the FN-051/054
/// aspect-format helper: an edit degrades cleanly when ffmpeg is absent, a foreign clip is rejected
/// (owner scope, BRD-81), an unsupported export format is a validation error, and the platform
/// formats map to the expected export dimensions.
/// </summary>
public class VideoEditorAndFormatTests
{
    private static readonly Guid OwnedAssetId = Guid.Parse("a5de0000-0000-4000-8000-000000000001");

    /// <summary>With ffmpeg absent the edit degrades cleanly with an actionable message (no crash, no export).</summary>
    [Fact]
    public async Task EditDegradesCleanlyWhenFfmpegAbsent()
    {
        var vEditor = BuildEditor(out var vAssets, ffmpegAvailable: false);

        var vResponse = await vEditor.EditAsync(new VideoEditRequest
        {
            SourceAssetId = OwnedAssetId,
            TrimStartSeconds = 0,
            TrimEndSeconds = 2,
            ExportFormat = "9:16"
        }, "owner", CancellationToken.None);

        Assert.NotNull(vResponse);
        Assert.Equal("degraded", vResponse!.Status);
        Assert.True(vResponse.Degraded);
        Assert.False(vResponse.FfmpegAvailable);
        Assert.Contains("ffmpeg", vResponse.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("export[mp4]", vResponse.Operations);
        await vAssets.DidNotReceiveWithAnyArgs().InsertAsync(default!, default);
    }

    /// <summary>A clip owned by another user is rejected (owner scope, BRD-81): the editor returns null → 404.</summary>
    [Fact]
    public async Task ForeignClipIsRejectedByOwnerScope()
    {
        var vEditor = BuildEditor(out _, ffmpegAvailable: false);

        var vResponse = await vEditor.EditAsync(new VideoEditRequest
        {
            SourceAssetId = OwnedAssetId,
            ExportFormat = "16:9"
        }, "another-user", CancellationToken.None);

        Assert.Null(vResponse);
    }

    /// <summary>An unsupported export format is a deliberate validation error.</summary>
    [Fact]
    public async Task UnsupportedExportFormatIsValidationError()
    {
        var vEditor = BuildEditor(out _, ffmpegAvailable: false);

        var vResponse = await vEditor.EditAsync(new VideoEditRequest
        {
            SourceAssetId = OwnedAssetId,
            ExportFormat = "21:9"
        }, "owner", CancellationToken.None);

        Assert.NotNull(vResponse);
        Assert.Equal("error", vResponse!.Status);
    }

    /// <summary>The platform aspect formats map to the expected 1080-based export dimensions.</summary>
    [Theory]
    [InlineData("16:9", 1920, 1080)]
    [InlineData("9:16", 1080, 1920)]
    [InlineData("1:1", 1080, 1080)]
    [InlineData("4:5", 1080, 1350)]
    public void FormatMapsToExpectedDimensions(string aFormat, int aWidth, int aHeight)
    {
        Assert.Equal(aFormat, VideoFormat.Normalize(aFormat));
        var (vWidth, vHeight) = VideoFormat.Dimensions(aFormat);
        Assert.Equal(aWidth, vWidth);
        Assert.Equal(aHeight, vHeight);
    }

    /// <summary>An unknown format normalises to null; duration/motion clamp to the accepted range.</summary>
    [Fact]
    public void NormalizeAndClampGuardControls()
    {
        Assert.Null(VideoFormat.Normalize("21:9"));
        Assert.Equal(VideoFormat.MaxDurationSeconds, VideoFormat.ClampDuration(9999d));
        Assert.Equal(VideoFormat.MinDurationSeconds, VideoFormat.ClampDuration(0d));
        Assert.Equal(VideoFormat.MaxMotion, VideoFormat.ClampMotion(999d));
        Assert.Equal(VideoFormat.MinMotion, VideoFormat.ClampMotion(-5d));
    }

    private static VideoEditor BuildEditor(out IAssetRepository aAssets, bool ffmpegAvailable)
    {
        var vAssets = Substitute.For<IAssetRepository>();
        // The seed clip is owned by "owner" only; any other caller sees null (owner scope).
        vAssets.GetAsync(OwnedAssetId, "owner", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<GeneratedAsset?>(new GeneratedAsset
            {
                AssetId = OwnedAssetId,
                UserId = "owner",
                Kind = "video",
                StoragePath = "/tmp/nonexistent-source.mp4"
            }));

        var vFfmpeg = Substitute.For<IFfmpegRunner>();
        vFfmpeg.IsAvailable.Returns(ffmpegAvailable);

        aAssets = vAssets;
        return new VideoEditor(vAssets, Substitute.For<IAssetStorage>(), vFfmpeg, NullLogger<VideoEditor>.Instance);
    }
}
