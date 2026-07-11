namespace TrStudio.Engine.Domain.Image;

/// <summary>
/// Options for the local ONNX image provider, bound from the <c>Image</c> configuration section
/// (REQ-FN-019). Controls where installed ONNX model weights live and whether the deterministic
/// synthetic fallback is permitted when no real model is installed.
/// </summary>
public class ImageProviderOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Image";

    /// <summary>
    /// The root directory holding installed ONNX model files. A model's file is resolved as
    /// <c>&lt;OnnxModelsRoot&gt;/&lt;WorkflowRef or ModelId&gt;.onnx</c>. When unset or the file
    /// is absent, the provider reports an actionable failure (or falls back to synthetic
    /// rendering when <see cref="SyntheticFallbackEnabled"/> is <c>true</c>).
    /// </summary>
    public string? OnnxModelsRoot { get; set; }

    /// <summary>
    /// When <c>true</c> (local dev only), a missing/unloadable model renders a deterministic,
    /// seed-driven synthetic image instead of failing, so the image vertical is exercisable
    /// end-to-end without multi-GB weights. Defaults to <c>false</c> (production surfaces a
    /// remediation-carrying failure instead).
    /// </summary>
    public bool SyntheticFallbackEnabled { get; set; }
}
