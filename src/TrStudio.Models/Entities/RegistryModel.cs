using TrStudio.Models.Enums;

namespace TrStudio.Models.Entities;

/// <summary>
/// A row of the model and provider registry owned by TrStudioApi (Architecture §6).
/// Every usable model is a registry row; the declarative <see cref="Params"/> schema lets
/// the UI render controls for any model with no code change.
/// </summary>
public class RegistryModel
{
    /// <summary>Stable registry identifier (e.g. "musetalk-v1").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable display name shown in model pickers.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The generation task this model performs.</summary>
    public GenerationTaskType Task { get; set; }

    /// <summary>The provider implementation family that executes this model's jobs.</summary>
    public ProviderKind ProviderKind { get; set; }

    /// <summary>The execution tier this model runs on (Fast / High / Premium).</summary>
    public GenerationTier Tier { get; set; }

    /// <summary>Reference to the ComfyUI workflow template for ComfyUI-backed models, if any.</summary>
    public string? WorkflowRef { get; set; }

    /// <summary>Declared hardware requirements for local/serverless execution.</summary>
    public HardwareRequirements HardwareRequirements { get; set; } = new();

    /// <summary>Declarative parameter specs the UI renders as controls.</summary>
    public List<ModelParamSpec> Params { get; set; } = new();

    /// <summary>Languages supported by the model (e.g. "en", "hi", "language-agnostic").</summary>
    public string[] Languages { get; set; } = Array.Empty<string>();

    /// <summary>The model licence name; verified by the build-time licence check (ADR-006).</summary>
    public string License { get; set; } = string.Empty;

    /// <summary>Safety classification scoping the safety floors applied to this model's jobs.</summary>
    public SafetyClass SafetyClass { get; set; }

    /// <summary>Cost declaration used by the cost estimator and spend guardrails.</summary>
    public ModelCost Cost { get; set; } = new();

    /// <summary>Whether the model's weights/workflow are installed and runnable on this instance.</summary>
    public bool IsReady { get; set; }

    /// <summary>Whether an admin has enabled the model for creators.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Chat-completion endpoint base URL for a <see cref="GenerationTaskType.TextChat"/> row
    /// (F-PROMPT / BRD-89). Null for generation models. The picked endpoint is resolved per chat
    /// call by <c>PromptChatService</c> (ADR-013); the API key is held in the encrypted key store
    /// under this row's <see cref="Id"/>, never on the row itself.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Underlying model name passed to the chat endpoint for a text-chat row (e.g. "gpt-4o",
    /// "claude-sonnet-5"). Null for generation models.
    /// </summary>
    public string? ChatModel { get; set; }

    /// <summary>
    /// Whether a text-chat row accepts image attachments (vision-capable). Gates the Prompt Studio
    /// attachment control (BRD-90); non-vision rows reject an attached image with a clear message.
    /// Always false for generation models.
    /// </summary>
    public bool VisionCapable { get; set; }
}
