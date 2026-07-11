using TrStudio.Engine.Domain.Image;

namespace TrStudio.Tests.Unit.Image;

/// <summary>
/// Unit tests for <see cref="ImageProcessor"/> (REQ-FN-022/023/024): deterministic synthetic
/// rendering underpinning seed reproducibility, 2K/4K upscaling, social-format export cropping
/// and background removal.
/// </summary>
public class ImageProcessorTests
{
    private readonly ImageProcessor objProcessor = new();

    /// <summary>
    /// The synthetic renderer is a pure function of its inputs: identical seed + signature yield
    /// byte-identical PNGs (REQ-FN-023 reproducibility), while a different seed diverges.
    /// </summary>
    [Fact]
    public void RenderSyntheticIsDeterministicPerSeed()
    {
        var vFirst = objProcessor.RenderSynthetic(128, 128, 42, "a red cube");
        var vSecond = objProcessor.RenderSynthetic(128, 128, 42, "a red cube");
        var vDifferentSeed = objProcessor.RenderSynthetic(128, 128, 43, "a red cube");

        Assert.Equal(vFirst, vSecond);
        Assert.NotEqual(vFirst, vDifferentSeed);
    }

    /// <summary>
    /// The synthetic renderer emits a valid PNG at the requested dimensions.
    /// </summary>
    [Fact]
    public void RenderSyntheticProducesRequestedDimensions()
    {
        var vBytes = objProcessor.RenderSynthetic(320, 200, 7, "landscape");

        var (vWidth, vHeight) = objProcessor.Probe(vBytes);
        Assert.Equal(320, vWidth);
        Assert.Equal(200, vHeight);
    }

    /// <summary>
    /// Upscaling scales the long edge up to the requested 2K/4K target (REQ-FN-022).
    /// </summary>
    [Fact]
    public async Task UpscaleReachesTargetLongEdge()
    {
        var vSource = objProcessor.RenderSynthetic(64, 48, 1, "seed");

        var vUpscaled = await objProcessor.UpscaleAsync(vSource, 2048, aFaceRestore: true);

        var (vWidth, vHeight) = objProcessor.Probe(vUpscaled);
        Assert.Equal(2048, Math.Max(vWidth, vHeight));
    }

    /// <summary>
    /// Exporting to a social preset centre-crops to that aspect ratio (REQ-FN-024).
    /// </summary>
    [Fact]
    public async Task ExportPresetCropsToAspectRatio()
    {
        var vSource = objProcessor.RenderSynthetic(1000, 1000, 3, "square");

        var vExported = await objProcessor.ExportPresetAsync(vSource, "16:9");

        var (vWidth, vHeight) = objProcessor.Probe(vExported);
        var vAspect = (double)vWidth / vHeight;
        Assert.InRange(vAspect, 16d / 9d - 0.05, 16d / 9d + 0.05);
    }

    /// <summary>
    /// An unsupported export preset is rejected rather than silently mishandled.
    /// </summary>
    [Fact]
    public async Task ExportPresetRejectsUnknownPreset()
    {
        var vSource = objProcessor.RenderSynthetic(100, 100, 3, "square");

        await Assert.ThrowsAsync<ArgumentException>(() => objProcessor.ExportPresetAsync(vSource, "banana"));
    }

    /// <summary>
    /// Background removal returns a valid same-size image and actually alters the pixels
    /// (edge-connected background pixels become transparent) — REQ-FN-024.
    /// </summary>
    [Fact]
    public async Task RemoveBackgroundReturnsAlteredSameSizeImage()
    {
        var vSource = objProcessor.RenderSynthetic(96, 96, 5, "subject");

        var vResult = await objProcessor.RemoveBackgroundAsync(vSource, aTolerance: 48);

        var (vWidth, vHeight) = objProcessor.Probe(vResult);
        Assert.Equal(96, vWidth);
        Assert.Equal(96, vHeight);
        Assert.NotEqual(vSource, vResult);
    }
}
