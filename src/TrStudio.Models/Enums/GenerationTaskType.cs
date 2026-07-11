namespace TrStudio.Models.Enums;

/// <summary>
/// The kind of generation work a job or registry model performs.
/// </summary>
/// <remarks>
/// Mirrors the <c>task</c> field of the model registry (Architecture §6). The orchestrator
/// resolves a task + model pair to exactly one provider (Architecture §5).
/// </remarks>
public enum GenerationTaskType
{
    /// <summary>Generate an image from a text prompt.</summary>
    TextToImage,

    /// <summary>Generate an image conditioned on a source image.</summary>
    ImageToImage,

    /// <summary>Regenerate a masked region of an image.</summary>
    Inpaint,

    /// <summary>Increase the resolution of an existing image.</summary>
    Upscale,

    /// <summary>Remove the background from an image.</summary>
    BackgroundRemove,

    /// <summary>Synchronise a face video/image to an audio track.</summary>
    LipSync,

    /// <summary>Synthesise speech audio from text.</summary>
    TextToSpeech,

    /// <summary>Generate a video clip from a source image.</summary>
    ImageToVideo,

    /// <summary>Generate a video clip from a text prompt.</summary>
    TextToVideo,

    /// <summary>Run an arbitrary registered ComfyUI workflow.</summary>
    WorkflowRun,

    /// <summary>
    /// Conversational text LLM used by the Prompt Studio to craft generation prompts
    /// (F-PROMPT / BRD-88..92). Served synchronously via <c>PromptChatService</c> (ADR-013),
    /// deliberately outside the <see cref="TrStudio.Models.Providers.IGenerationProvider"/>
    /// job spine — a text-chat row is never resolved to a generation provider.
    /// </summary>
    TextChat
}
