namespace TrStudio.Engine.Domain.Comfy;

/// <summary>
/// The set of ComfyUI node class types that ship with a stock ComfyUI install, plus the input
/// names that reference on-disk model files. Used by <see cref="ComfyDependencyScanner"/> to flag
/// class types outside this set as custom nodes needing installation (FN-047/BRD-38).
/// </summary>
public static class ComfyBuiltinNodes
{
    /// <summary>Stock ComfyUI node class types (a pragmatic core set; anything else is treated as a custom node).</summary>
    public static readonly IReadOnlySet<string> Builtin = new HashSet<string>(StringComparer.Ordinal)
    {
        "KSampler", "KSamplerAdvanced", "SamplerCustom", "BasicScheduler",
        "CLIPTextEncode", "CLIPSetLastLayer", "CLIPVisionEncode",
        "CheckpointLoaderSimple", "CheckpointLoader", "UNETLoader", "VAELoader", "VAELoaderKJ",
        "LoraLoader", "LoraLoaderModelOnly", "ControlNetLoader", "ControlNetApply",
        "ControlNetApplyAdvanced", "CLIPLoader", "DualCLIPLoader", "StyleModelLoader",
        "UpscaleModelLoader", "GLIGENLoader",
        "VAEDecode", "VAEEncode", "VAEEncodeForInpaint",
        "EmptyLatentImage", "LatentUpscale", "LatentUpscaleBy", "LatentFromBatch", "RepeatLatentBatch",
        "SaveImage", "PreviewImage", "LoadImage", "LoadImageMask",
        "ImageScale", "ImageScaleBy", "ImageUpscaleWithModel", "ImageBlend", "ImageInvert",
        "ConditioningCombine", "ConditioningConcat", "ConditioningSetArea", "ConditioningZeroOut",
        "ConditioningAverage", "ConditioningSetTimestepRange",
        "PositiveConditioning", "NegativeConditioning",
        "ModelSamplingDiscrete", "ModelSamplingSD3", "FreeU", "FreeU_V2",
        "InpaintModelConditioning", "SetLatentNoiseMask", "VAEDecodeTiled"
    };

    /// <summary>Input field names whose scalar value names an on-disk model file to be checked for availability.</summary>
    public static readonly IReadOnlySet<string> ModelFileInputs = new HashSet<string>(StringComparer.Ordinal)
    {
        "ckpt_name", "vae_name", "lora_name", "control_net_name", "unet_name",
        "clip_name", "clip_name1", "clip_name2", "model_name", "style_model_name",
        "upscale_model_name", "gligen_name", "embedding"
    };

    /// <summary>Whether a class type is part of the stock ComfyUI install.</summary>
    /// <param name="aClassType">The node class type to test.</param>
    /// <returns><c>true</c> when the class type is built in; otherwise <c>false</c>.</returns>
    public static bool IsBuiltin(string aClassType) => Builtin.Contains(aClassType);

    /// <summary>Whether an input field references an on-disk model file.</summary>
    /// <param name="aInputName">The input field name to test.</param>
    /// <returns><c>true</c> when the input names a model file; otherwise <c>false</c>.</returns>
    public static bool IsModelFileInput(string aInputName) => ModelFileInputs.Contains(aInputName);
}
