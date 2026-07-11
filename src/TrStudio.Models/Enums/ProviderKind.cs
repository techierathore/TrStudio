namespace TrStudio.Models.Enums;

/// <summary>
/// The concrete provider implementation family that executes a registry model's jobs.
/// </summary>
/// <remarks>
/// Each value maps to one <see cref="TrStudio.Models.Providers.IGenerationProvider"/>
/// implementation in <c>TrStudio.Engine/Providers</c> (Architecture §5).
/// </remarks>
public enum ProviderKind
{
    /// <summary>In-process ONNX Runtime provider (local image path, Fast tier).</summary>
    OnnxLocal,

    /// <summary>ComfyUI workflow provider (local MPS or RunPod serverless).</summary>
    ComfyUi,

    /// <summary>HeyGen API adapter (Premium avatar tier).</summary>
    HeyGen,

    /// <summary>Closed-API aggregator adapter (fal.ai / Replicate / WaveSpeed or direct vendor).</summary>
    ClosedApi,

    /// <summary>RunPod serverless GPU provider (scale-to-zero, High tier — REQ-FN-028/BRD-12).</summary>
    RunPod,

    /// <summary>In-process stub provider producing a placeholder asset; used to verify the spine end-to-end (dev-seed).</summary>
    StubEcho,

    /// <summary>Text-to-speech / voice-cloning provider family (FN-031..034/BRD-22..25); routes per language behind one voice control.</summary>
    Voice,

    /// <summary>
    /// Conversational text LLM family for the Prompt Studio (F-PROMPT / BRD-89, ADR-013).
    /// Served by <c>PromptChatService</c> via TechieRag direct chat-completion — NOT an
    /// <see cref="TrStudio.Models.Providers.IGenerationProvider"/>; excluded from generation-job resolution.
    /// </summary>
    TextLlm
}
