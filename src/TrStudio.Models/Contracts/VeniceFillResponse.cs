using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// What "Fill from Venice" found for one Venice model in Venice's public model list. Nothing is
/// saved: the admin checks the values and presses Save (decision 4B, BRD-122).
/// </summary>
public class VeniceFillResponse
{
    /// <summary>The Venice model id that was looked up.</summary>
    public string ProviderModelId { get; set; } = string.Empty;

    /// <summary>Venice's display name for the model.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The task the model performs in TrStudio terms.</summary>
    public GenerationTaskType Task { get; set; }

    /// <summary>Whether the model takes a reference (or source) picture.</summary>
    public bool TakesReferencePicture { get; set; }

    /// <summary>Whether the model keeps a fixed character (a reference-to-video model).</summary>
    public bool KeepsFixedCharacter { get; set; }

    /// <summary>The most input pictures Venice states for the model, if any.</summary>
    public int? MaxInputImages { get; set; }

    /// <summary>Allowed durations (video), e.g. "5s".</summary>
    public string[] Durations { get; set; } = Array.Empty<string>();

    /// <summary>Allowed resolutions, e.g. "480p" or "1K".</summary>
    public string[] Resolutions { get; set; } = Array.Empty<string>();

    /// <summary>Allowed aspect ratios, e.g. "16:9".</summary>
    public string[] AspectRatios { get; set; } = Array.Empty<string>();

    /// <summary>Venice's listed US-dollar price for the cheapest setting, when the list states one (pictures only).</summary>
    public decimal? ListedPriceUsd { get; set; }
}
