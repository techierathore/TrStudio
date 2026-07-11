using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixImage = SixLabors.ImageSharp.Image;

namespace TrStudio.Engine.Domain.Image;

/// <summary>
/// ImageSharp-backed <see cref="IImageProcessor"/>. Every operation is a deterministic pure
/// function of its inputs (fixed resamplers, fixed PNG encoder settings, coordinate-hashed
/// synthesis) so identical inputs always produce byte-identical output — the foundation of seed
/// reproducibility (REQ-FN-023).
/// </summary>
public class ImageProcessor : IImageProcessor
{
    private static readonly PngEncoder PngEncoderInstance = new()
    {
        CompressionLevel = PngCompressionLevel.BestCompression,
        ColorType = PngColorType.RgbWithAlpha,
        BitDepth = PngBitDepth.Bit8
    };

    /// <inheritdoc />
    public byte[] RenderSynthetic(int aWidth, int aHeight, long aSeed, string aSignature, byte[]? aSourceBytes = null, byte[]? aMaskBytes = null)
    {
        var vWidth = Math.Clamp(aWidth, 8, 4096);
        var vHeight = Math.Clamp(aHeight, 8, 4096);
        var vSignatureHash = Fnv1a64(aSignature);
        var vSeed = unchecked((ulong)aSeed) ^ vSignatureHash;

        var vSource = aSourceBytes is null ? null : LoadResized(aSourceBytes, vWidth, vHeight);
        var vMask = aMaskBytes is null ? null : LoadResized(aMaskBytes, vWidth, vHeight);

        try
        {
            using var vImage = new Image<Rgba32>(vWidth, vHeight);
            var vSourceImage = vSource;
            var vMaskImage = vMask;

            vImage.ProcessPixelRows(aAccessor =>
            {
                for (var vY = 0; vY < vHeight; vY++)
                {
                    var vRow = aAccessor.GetRowSpan(vY);
                    for (var vX = 0; vX < vWidth; vX++)
                    {
                        var vHash = Hash((uint)vX, (uint)vY, vSeed);
                        var vSynthR = (byte)(((vX * 256 / vWidth) * 3 + (vHash & 0xFF)) / 4);
                        var vSynthG = (byte)(((vY * 256 / vHeight) * 3 + ((vHash >> 8) & 0xFF)) / 4);
                        var vSynthB = (byte)((vHash >> 16) & 0xFF);

                        if (vSourceImage is null)
                        {
                            vRow[vX] = new Rgba32(vSynthR, vSynthG, vSynthB, 255);
                            continue;
                        }

                        var vSourcePixel = vSourceImage[vX, vY];
                        var vApply = true;
                        if (vMaskImage is not null)
                        {
                            var vMaskPixel = vMaskImage[vX, vY];
                            var vLuminance = (vMaskPixel.R + vMaskPixel.G + vMaskPixel.B) / 3;
                            vApply = vLuminance > 128;
                        }

                        if (!vApply)
                        {
                            vRow[vX] = vSourcePixel;
                            continue;
                        }

                        vRow[vX] = new Rgba32(
                            (byte)((vSourcePixel.R * 3 + vSynthR * 2) / 5),
                            (byte)((vSourcePixel.G * 3 + vSynthG * 2) / 5),
                            (byte)((vSourcePixel.B * 3 + vSynthB * 2) / 5),
                            255);
                    }
                }
            });

            return Encode(vImage);
        }
        finally
        {
            vSource?.Dispose();
            vMask?.Dispose();
        }
    }

    /// <inheritdoc />
    public Task<byte[]> UpscaleAsync(byte[] aSource, int aTargetLongEdge, bool aFaceRestore, CancellationToken aCancellationToken = default)
    {
        using var vImage = SixImage.Load<Rgba32>(aSource);
        var vTarget = Math.Clamp(aTargetLongEdge, 1024, 4096);
        var vLongEdge = Math.Max(vImage.Width, vImage.Height);
        var vScale = (double)vTarget / vLongEdge;
        var vNewWidth = Math.Max(1, (int)Math.Round(vImage.Width * vScale));
        var vNewHeight = Math.Max(1, (int)Math.Round(vImage.Height * vScale));

        vImage.Mutate(aContext => aContext.Resize(new ResizeOptions
        {
            Size = new Size(vNewWidth, vNewHeight),
            Sampler = KnownResamplers.Lanczos3,
            Mode = ResizeMode.Stretch
        }));

        if (aFaceRestore)
        {
            vImage.Mutate(aContext => aContext.GaussianSharpen(0.6f));
        }

        return Task.FromResult(Encode(vImage));
    }

    /// <inheritdoc />
    public Task<byte[]> ExportPresetAsync(byte[] aSource, string aPreset, CancellationToken aCancellationToken = default)
    {
        var (vAspectWidth, vAspectHeight) = ParsePreset(aPreset);
        using var vImage = SixImage.Load<Rgba32>(aSource);

        var vTargetAspect = (double)vAspectWidth / vAspectHeight;
        var vSourceAspect = (double)vImage.Width / vImage.Height;

        int vCropWidth;
        int vCropHeight;
        if (vSourceAspect > vTargetAspect)
        {
            vCropHeight = vImage.Height;
            vCropWidth = Math.Max(1, (int)Math.Round(vImage.Height * vTargetAspect));
        }
        else
        {
            vCropWidth = vImage.Width;
            vCropHeight = Math.Max(1, (int)Math.Round(vImage.Width / vTargetAspect));
        }

        var vX = (vImage.Width - vCropWidth) / 2;
        var vY = (vImage.Height - vCropHeight) / 2;
        vImage.Mutate(aContext => aContext.Crop(new Rectangle(vX, vY, vCropWidth, vCropHeight)));

        return Task.FromResult(Encode(vImage));
    }

    /// <inheritdoc />
    public Task<byte[]> RemoveBackgroundAsync(byte[] aSource, int aTolerance, CancellationToken aCancellationToken = default)
    {
        var vTolerance = Math.Clamp(aTolerance, 0, 255);
        using var vImage = SixImage.Load<Rgba32>(aSource);
        var vWidth = vImage.Width;
        var vHeight = vImage.Height;
        var vPixels = new Rgba32[vWidth * vHeight];
        vImage.CopyPixelDataTo(vPixels);

        var vBackground = AverageCorners(vPixels, vWidth, vHeight);
        var vVisited = new bool[vWidth * vHeight];
        var vQueue = new Queue<int>();

        for (var vX = 0; vX < vWidth; vX++)
        {
            EnqueueBackground(vQueue, vVisited, vPixels, vX, vWidth, vBackground, vTolerance);
            EnqueueBackground(vQueue, vVisited, vPixels, (vHeight - 1) * vWidth + vX, vWidth, vBackground, vTolerance);
        }

        for (var vY = 0; vY < vHeight; vY++)
        {
            EnqueueBackground(vQueue, vVisited, vPixels, vY * vWidth, vWidth, vBackground, vTolerance);
            EnqueueBackground(vQueue, vVisited, vPixels, vY * vWidth + (vWidth - 1), vWidth, vBackground, vTolerance);
        }

        while (vQueue.Count > 0)
        {
            var vIndex = vQueue.Dequeue();
            vPixels[vIndex] = new Rgba32(vPixels[vIndex].R, vPixels[vIndex].G, vPixels[vIndex].B, 0);

            var vX = vIndex % vWidth;
            var vY = vIndex / vWidth;
            if (vX > 0) EnqueueBackground(vQueue, vVisited, vPixels, vIndex - 1, vWidth, vBackground, vTolerance);
            if (vX < vWidth - 1) EnqueueBackground(vQueue, vVisited, vPixels, vIndex + 1, vWidth, vBackground, vTolerance);
            if (vY > 0) EnqueueBackground(vQueue, vVisited, vPixels, vIndex - vWidth, vWidth, vBackground, vTolerance);
            if (vY < vHeight - 1) EnqueueBackground(vQueue, vVisited, vPixels, vIndex + vWidth, vWidth, vBackground, vTolerance);
        }

        using var vResult = SixImage.LoadPixelData<Rgba32>(vPixels, vWidth, vHeight);
        return Task.FromResult(Encode(vResult));
    }

    /// <inheritdoc />
    public (int Width, int Height) Probe(byte[] aBytes)
    {
        var vInfo = SixImage.Identify(aBytes);
        return (vInfo.Width, vInfo.Height);
    }

    private static void EnqueueBackground(Queue<int> aQueue, bool[] aVisited, Rgba32[] aPixels, int aIndex, int aWidth, Rgba32 aBackground, int aTolerance)
    {
        if (aVisited[aIndex])
        {
            return;
        }

        aVisited[aIndex] = true;
        if (ColorDistance(aPixels[aIndex], aBackground) <= aTolerance)
        {
            aQueue.Enqueue(aIndex);
        }
    }

    private static int ColorDistance(Rgba32 aFirst, Rgba32 aSecond)
        => (Math.Abs(aFirst.R - aSecond.R) + Math.Abs(aFirst.G - aSecond.G) + Math.Abs(aFirst.B - aSecond.B)) / 3;

    private static Rgba32 AverageCorners(Rgba32[] aPixels, int aWidth, int aHeight)
    {
        var vTopLeft = aPixels[0];
        var vTopRight = aPixels[aWidth - 1];
        var vBottomLeft = aPixels[(aHeight - 1) * aWidth];
        var vBottomRight = aPixels[aHeight * aWidth - 1];
        return new Rgba32(
            (byte)((vTopLeft.R + vTopRight.R + vBottomLeft.R + vBottomRight.R) / 4),
            (byte)((vTopLeft.G + vTopRight.G + vBottomLeft.G + vBottomRight.G) / 4),
            (byte)((vTopLeft.B + vTopRight.B + vBottomLeft.B + vBottomRight.B) / 4),
            255);
    }

    private static (int Width, int Height) ParsePreset(string aPreset)
    {
        var vParts = (aPreset ?? string.Empty).Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (vParts.Length == 2
            && int.TryParse(vParts[0], out var vWidth) && vWidth > 0
            && int.TryParse(vParts[1], out var vHeight) && vHeight > 0)
        {
            return (vWidth, vHeight);
        }

        throw new ArgumentException($"Unsupported export preset '{aPreset}'. Use one of 1:1, 4:5, 9:16, 16:9.", nameof(aPreset));
    }

    private static Image<Rgba32> LoadResized(byte[] aBytes, int aWidth, int aHeight)
    {
        var vImage = SixImage.Load<Rgba32>(aBytes);
        vImage.Mutate(aContext => aContext.Resize(new ResizeOptions
        {
            Size = new Size(aWidth, aHeight),
            Sampler = KnownResamplers.Lanczos3,
            Mode = ResizeMode.Stretch
        }));
        return vImage;
    }

    private static byte[] Encode(Image<Rgba32> aImage)
    {
        using var vStream = new MemoryStream();
        aImage.Save(vStream, PngEncoderInstance);
        return vStream.ToArray();
    }

    private static uint Hash(uint aX, uint aY, ulong aSeed)
    {
        unchecked
        {
            var vValue = aSeed ^ ((ulong)aX * 0x9E3779B97F4A7C15UL) ^ ((ulong)aY * 0xC2B2AE3D27D4EB4FUL);
            vValue ^= vValue >> 33;
            vValue *= 0xFF51AFD7ED558CCDUL;
            vValue ^= vValue >> 33;
            return (uint)(vValue & 0xFFFFFFFFUL);
        }
    }

    private static ulong Fnv1a64(string aValue)
    {
        unchecked
        {
            var vHash = 0xCBF29CE484222325UL;
            foreach (var vByte in Encoding.UTF8.GetBytes(aValue ?? string.Empty))
            {
                vHash ^= vByte;
                vHash *= 0x100000001B3UL;
            }

            return vHash;
        }
    }
}
