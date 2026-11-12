using TrStudio.Models.Entities;
using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// A creator-facing model-picker row returned by <c>GET /api/v1/models</c> (phase 2, ADR-014). It
/// carries what the studios need to filter and render a model — task, provider kind, the two
/// ability flags and the allowed durations, resolutions and aspect ratios — but never the
/// provider's own model id, endpoint or any key.
/// </summary>
public class CatalogModelResponse
{
    /// <summary>Stable registry identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The generation task this model performs.</summary>
    public GenerationTaskType Task { get; set; }

    /// <summary>The provider family (e.g. <see cref="ProviderKind.Venice"/>), for the "Cloud · Venice" badge.</summary>
    public ProviderKind ProviderKind { get; set; }

    /// <summary>The execution tier.</summary>
    public GenerationTier Tier { get; set; }

    /// <summary>Reference to a ComfyUI workflow template, if any (unchanged from the registry row).</summary>
    public string? WorkflowRef { get; set; }

    /// <summary>Declared hardware requirements.</summary>
    public HardwareRequirements HardwareRequirements { get; set; } = new();

    /// <summary>The declarative params schema the studio renders.</summary>
    public List<ModelParamSpec> Params { get; set; } = new();

    /// <summary>Supported languages.</summary>
    public string[] Languages { get; set; } = Array.Empty<string>();

    /// <summary>The model licence name.</summary>
    public string License { get; set; } = string.Empty;

    /// <summary>Safety classification.</summary>
    public SafetyClass SafetyClass { get; set; }

    /// <summary>Cost declaration.</summary>
    public ModelCost Cost { get; set; } = new();

    /// <summary>Whether the model is runnable on this instance.</summary>
    public bool IsReady { get; set; }

    /// <summary>Whether the model is enabled for creators (always true in this listing).</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Whether the model takes a reference (or source) picture (BRD-97, BRD-101).</summary>
    public bool TakesReferencePicture { get; set; }

    /// <summary>Whether the model keeps a fixed character (BRD-97); a chosen character offers only these.</summary>
    public bool KeepsFixedCharacter { get; set; }

    /// <summary>The most input pictures the model accepts, when stated.</summary>
    public int? MaxInputImages { get; set; }

    /// <summary>Whether a masked edit may be offered to this model (false for every Venice row, BRD-103).</summary>
    public bool SupportsMask { get; set; }

    /// <summary>Whether the prompt and pictures leave this machine for a cloud provider (BRD-117 notice).</summary>
    public bool IsCloud { get; set; }

    /// <summary>Allowed clip durations (e.g. "5s"), from the params schema key <c>duration</c>; empty when unconstrained.</summary>
    public string[] Durations { get; set; } = Array.Empty<string>();

    /// <summary>Allowed resolutions (e.g. "480p", "1K"), from the params schema key <c>resolution</c>.</summary>
    public string[] Resolutions { get; set; } = Array.Empty<string>();

    /// <summary>Allowed aspect ratios (e.g. "16:9"), from the params schema key <c>aspectRatio</c>.</summary>
    public string[] AspectRatios { get; set; } = Array.Empty<string>();
}
