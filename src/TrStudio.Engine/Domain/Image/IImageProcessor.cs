namespace TrStudio.Engine.Domain.Image;

/// <summary>
/// Deterministic image operations backing the image vertical (REQ-FN-019/022/023/024). All
/// methods are pure functions of their inputs: identical inputs always yield byte-identical
/// output, which underpins seed reproducibility (REQ-FN-023).
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Renders a deterministic, seed-driven synthetic PNG. Given identical arguments the output
    /// bytes are identical, so the same seed + params reproduce the same image (REQ-FN-023) and
    /// unit tests can exercise the provider without a real ONNX model (REQ-FN-019).
    /// </summary>
    /// <param name="aWidth">Output width in pixels.</param>
    /// <param name="aHeight">Output height in pixels.</param>
    /// <param name="aSeed">The reproducibility seed.</param>
    /// <param name="aSignature">A stable string derived from the prompt/params that also drives the pattern.</param>
    /// <param name="aSourceBytes">Optional source image bytes for image-to-image conditioning.</param>
    /// <param name="aMaskBytes">Optional mask bytes for inpaint (only masked regions are altered).</param>
    /// <returns>The rendered PNG bytes.</returns>
    byte[] RenderSynthetic(int aWidth, int aHeight, long aSeed, string aSignature, byte[]? aSourceBytes = null, byte[]? aMaskBytes = null);

    /// <summary>
    /// Upscales an image so its long edge equals <paramref name="aTargetLongEdge"/> (2K/4K),
    /// optionally applying a face-restoration sharpen pass (REQ-FN-022).
    /// </summary>
    /// <param name="aSource">The source image bytes.</param>
    /// <param name="aTargetLongEdge">The desired long-edge resolution in pixels (clamped 1024–4096).</param>
    /// <param name="aFaceRestore">Whether to apply a detail-preserving sharpen after upscaling.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The upscaled PNG bytes.</returns>
    Task<byte[]> UpscaleAsync(byte[] aSource, int aTargetLongEdge, bool aFaceRestore, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Centre-crops an image to a social-format aspect preset (<c>1:1</c>, <c>4:5</c>,
    /// <c>9:16</c>, <c>16:9</c>) at native resolution (REQ-FN-024).
    /// </summary>
    /// <param name="aSource">The source image bytes.</param>
    /// <param name="aPreset">The aspect preset in <c>w:h</c> form.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The cropped PNG bytes.</returns>
    Task<byte[]> ExportPresetAsync(byte[] aSource, string aPreset, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Removes the (edge-connected) background of an image, producing an RGBA PNG with a
    /// transparent background (REQ-FN-024).
    /// </summary>
    /// <param name="aSource">The source image bytes.</param>
    /// <param name="aTolerance">Colour-distance tolerance (0–255) for background matching.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The background-removed RGBA PNG bytes.</returns>
    Task<byte[]> RemoveBackgroundAsync(byte[] aSource, int aTolerance, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Reads the pixel dimensions of an encoded image without fully decoding it.
    /// </summary>
    /// <param name="aBytes">The encoded image bytes.</param>
    /// <returns>The width and height in pixels.</returns>
    (int Width, int Height) Probe(byte[] aBytes);
}
