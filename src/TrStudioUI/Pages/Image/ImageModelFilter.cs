using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;

namespace TrStudioUI.Pages.Image;

/// <summary>
/// Pure list rules for the Image studio model pickers (phase 2, Venice). Kept free of Razor so the
/// rules are unit-tested on their own (tests/unit/TrStudio.Tests.Unit/Venice/ImageModelFilterTests.cs).
/// </summary>
/// <remarks>
/// REQ-UI-085: picture rows (Venice included) are listed, Venice ones labelled "Cloud · Venice".
/// REQ-UI-087: a reference picture keeps only rows that take one.
/// REQ-UI-088: a drawn mask keeps only rows that support a mask, so no Venice row is offered.
/// REQ-FN-080: Venice upscale rows are offered beside the local upscaler.
/// </remarks>
public static class ImageModelFilter
{
    /// <summary>The value of the local (GFPGAN + ESRGAN) upscaler option.</summary>
    public const string LocalUpscalerId = "local";

    /// <summary>The label of the local upscaler option, as in the mockup.</summary>
    public const string LocalUpscalerLabel = "GFPGAN + ESRGAN (local)";

    /// <summary>The badge text shown beside a Venice row.</summary>
    public const string VeniceBadge = "Cloud · Venice";

    /// <summary>Whether a task produces a picture in the main model list of the Image studio.</summary>
    /// <param name="aTask">The registry row's task.</param>
    /// <returns>True for text-to-image, image-to-image and inpaint.</returns>
    public static bool IsPictureTask(GenerationTaskType aTask) =>
        aTask is GenerationTaskType.TextToImage or GenerationTaskType.ImageToImage or GenerationTaskType.Inpaint;

    /// <summary>Whether a catalog row is served by Venice.</summary>
    /// <param name="aModel">The catalog row.</param>
    /// <returns>True when the row's provider is Venice.</returns>
    public static bool IsVenice(CatalogModelResponse aModel) => aModel.ProviderKind == ProviderKind.Venice;

    /// <summary>
    /// The title over a failed run's own words: Venice's failures say Venice (BRD-113); any other
    /// provider's failure does not (owner run 16 Nov 2026: a local ONNX job on the Mac read "Venice could
    /// not make this").
    /// </summary>
    public static string FailureTitle(CatalogModelResponse? aModel)
        => aModel is not null && IsVenice(aModel) ? "Venice could not make this" : "The picture was not made";

    /// <summary>
    /// The rows the model Select offers: picture rows only; with a reference picture only rows that take
    /// one; with a drawn mask only rows that support a mask (never a Venice row).
    /// </summary>
    /// <param name="aModels">The enabled catalog from <c>GET /api/v1/models</c>.</param>
    /// <param name="aHasReferencePicture">Whether the creator added a reference picture.</param>
    /// <param name="aHasMask">Whether the creator drew a mask.</param>
    /// <returns>The filtered rows, in catalog order.</returns>
    public static IReadOnlyList<CatalogModelResponse> Filter(
        IEnumerable<CatalogModelResponse> aModels, bool aHasReferencePicture, bool aHasMask) =>
        aModels
            .Where(aModel => IsPictureTask(aModel.Task))
            .Where(aModel => !aHasReferencePicture || aModel.TakesReferencePicture)
            .Where(aModel => !aHasMask || (aModel.SupportsMask && !IsVenice(aModel)))
            .ToList();

    /// <summary>The Select label for a row: Venice rows read "Name — Cloud · Venice".</summary>
    /// <param name="aModel">The catalog row.</param>
    /// <returns>The option label.</returns>
    public static string OptionLabel(CatalogModelResponse aModel) =>
        IsVenice(aModel) ? $"{aModel.DisplayName} — {VeniceBadge}" : aModel.DisplayName;

    /// <summary>
    /// The Upscale model choices: the local upscaler first, then every Venice upscale row (REQ-FN-080).
    /// </summary>
    /// <param name="aModels">The enabled catalog.</param>
    /// <returns>(value, label) pairs for the upscale-model Select.</returns>
    public static IReadOnlyList<KeyValuePair<string, string>> UpscaleChoices(IEnumerable<CatalogModelResponse> aModels)
    {
        var vChoices = new List<KeyValuePair<string, string>> { new(LocalUpscalerId, LocalUpscalerLabel) };
        vChoices.AddRange(aModels
            .Where(aModel => aModel.Task == GenerationTaskType.Upscale && IsVenice(aModel))
            .Select(aModel => new KeyValuePair<string, string>(aModel.Id, OptionLabel(aModel))));
        return vChoices;
    }

    /// <summary>Maps the 2K / 4K upscale button to the Venice <c>scale</c> param (2 or 4).</summary>
    /// <param name="aUpscaleLabel">"2K" or "4K".</param>
    /// <returns>2 for "2K", otherwise 4.</returns>
    public static int ScaleOf(string aUpscaleLabel) => aUpscaleLabel == "2K" ? 2 : 4;

    /// <summary>
    /// Classifies a failed job submit so the page shows the right alert: 402 SPEND_CAP_EXCEEDED is the
    /// over-cap alert (REQ-FN-085); 502 PROVIDER_FAILED shows the provider's own text word for word (BRD-113).
    /// </summary>
    /// <param name="aStatusCode">The HTTP status of the submit answer.</param>
    /// <param name="aTitle">The problem title (the error code).</param>
    /// <param name="aDetail">The problem detail.</param>
    /// <returns>The outcome kind and the text to show.</returns>
    public static SubmitFailure ClassifySubmitFailure(int aStatusCode, string? aTitle, string? aDetail)
    {
        if (aStatusCode == 402 || aTitle == "SPEND_CAP_EXCEEDED")
        {
            return new SubmitFailure(SubmitFailureKind.OverCap,
                string.IsNullOrWhiteSpace(aDetail) ? "This month's spend cap is reached. Nothing was sent." : aDetail!);
        }

        if (aStatusCode == 502 || aTitle == "PROVIDER_FAILED")
        {
            return new SubmitFailure(SubmitFailureKind.ProviderFailed,
                string.IsNullOrWhiteSpace(aDetail) ? "The provider refused the request." : aDetail!);
        }

        return new SubmitFailure(SubmitFailureKind.Other,
            string.IsNullOrWhiteSpace(aDetail) ? $"The request failed (HTTP {aStatusCode})." : aDetail!);
    }
}

/// <summary>Which alert a failed submit raises on the Image studio.</summary>
public enum SubmitFailureKind
{
    /// <summary>Over the monthly spend cap (402 SPEND_CAP_EXCEEDED).</summary>
    OverCap,

    /// <summary>The provider refused the call (502 PROVIDER_FAILED); the text is the provider's own.</summary>
    ProviderFailed,

    /// <summary>Any other failure.</summary>
    Other
}

/// <summary>A classified submit failure: the alert kind and the text to show.</summary>
/// <param name="Kind">The alert kind.</param>
/// <param name="Message">The text shown in the alert.</param>
public sealed record SubmitFailure(SubmitFailureKind Kind, string Message);
