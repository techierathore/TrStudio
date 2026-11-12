using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;

namespace TrStudio.Engine.Domain.Registry;

/// <summary>
/// Maps a registry row to the creator-facing <see cref="CatalogModelResponse"/> (phase 2): the
/// provider kind, the two ability flags and the allowed durations, resolutions and aspect ratios
/// read from the params schema — never the provider's own model id, endpoint or key.
/// </summary>
public static class CatalogModelMapper
{
    /// <summary>Maps one row.</summary>
    /// <param name="aModel">The registry row.</param>
    /// <returns>The creator-facing picker row.</returns>
    public static CatalogModelResponse ToResponse(RegistryModel aModel) => new()
    {
        Id = aModel.Id,
        DisplayName = aModel.DisplayName,
        Task = aModel.Task,
        ProviderKind = aModel.ProviderKind,
        Tier = aModel.Tier,
        WorkflowRef = aModel.WorkflowRef,
        HardwareRequirements = aModel.HardwareRequirements,
        Params = aModel.Params,
        Languages = aModel.Languages,
        License = aModel.License,
        SafetyClass = aModel.SafetyClass,
        Cost = aModel.Cost,
        IsReady = aModel.IsReady,
        IsEnabled = aModel.IsEnabled,
        TakesReferencePicture = aModel.TakesReferencePicture,
        KeepsFixedCharacter = aModel.KeepsFixedCharacter,
        MaxInputImages = aModel.MaxInputImages,
        // Venice's edit endpoint has no mask (ADR-014): a masked edit is never offered to it (BRD-103).
        SupportsMask = aModel.ProviderKind != ProviderKind.Venice
                       && aModel.Task is GenerationTaskType.Inpaint or GenerationTaskType.ImageToImage or GenerationTaskType.TextToImage,
        IsCloud = aModel.ProviderKind is ProviderKind.Venice or ProviderKind.ClosedApi or ProviderKind.HeyGen,
        Durations = ValuesOf(aModel, "duration"),
        Resolutions = ValuesOf(aModel, "resolution"),
        AspectRatios = ValuesOf(aModel, "aspectRatio")
    };

    private static string[] ValuesOf(RegistryModel aModel, string aKey)
        => aModel.Params.FirstOrDefault(aSpec => string.Equals(aSpec.Key, aKey, StringComparison.OrdinalIgnoreCase))?.Values
           ?? Array.Empty<string>();
}
