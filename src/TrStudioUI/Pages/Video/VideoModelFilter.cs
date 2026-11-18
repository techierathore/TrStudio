using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;

namespace TrStudioUI.Pages.Video;

/// <summary>
/// Pure rules behind the Video studio's registry-driven model picker (REQ-UI-089..092, REQ-FN-081):
/// which catalog rows the picker lists for the chosen mode and character, and which duration,
/// resolution and aspect values the controls may offer for the picked row. No I/O, so it is unit
/// tested directly (tests/unit/TrStudio.Tests.Unit/Venice/VideoModelFilterTests.cs).
/// </summary>
public static class VideoModelFilter
{
    /// <summary>Durations offered when the picked row states none (the slider's 2–10 s range).</summary>
    public static readonly IReadOnlyList<string> DefaultDurations =
        Enumerable.Range(2, 9).Select(aSec => $"{aSec}s").ToArray();

    /// <summary>Aspect ratios offered when the picked row states none.</summary>
    public static readonly IReadOnlyList<string> DefaultAspectRatios = new[] { "16:9", "9:16", "1:1" };

    /// <summary>
    /// Lists the rows the picker shows: enabled rows whose task matches the mode and, when a character
    /// is chosen, only rows that keep a fixed character. Order follows the catalog.
    /// </summary>
    /// <param name="aModels">The creator catalog (<c>GET /api/v1/models</c>).</param>
    /// <param name="aImageToVideo">True for Image to video, false for Text to video.</param>
    /// <param name="aCharacterChosen">Whether a character is picked.</param>
    /// <returns>The rows to list.</returns>
    public static IReadOnlyList<CatalogModelResponse> ForStudio(
        IEnumerable<CatalogModelResponse> aModels, bool aImageToVideo, bool aCharacterChosen)
    {
        var vTask = aImageToVideo ? GenerationTaskType.ImageToVideo : GenerationTaskType.TextToVideo;
        return aModels
            .Where(aModel => aModel.IsEnabled && aModel.Task == vTask)
            .Where(aModel => !aCharacterChosen || aModel.KeepsFixedCharacter)
            .ToList();
    }

    /// <summary>Whether a row runs on Venice (shows the "Cloud · Venice" badge and notice).</summary>
    /// <param name="aModel">The row, or null.</param>
    public static bool IsVenice(CatalogModelResponse? aModel) => aModel?.ProviderKind == ProviderKind.Venice;

    /// <summary>
    /// The text a row shows in the Model box and its list: Venice rows read "Name — Cloud · Venice"
    /// (REQ-UI-089), others their own name. The closed box shows it in full, wrapping (owner screen
    /// check, 17 Nov 2026: a long name was cut at 1280 px).
    /// </summary>
    /// <param name="aModel">The row.</param>
    public static string OptionLabel(CatalogModelResponse aModel)
        => IsVenice(aModel) ? $"{aModel.DisplayName} — Cloud · Venice" : aModel.DisplayName;

    /// <summary>
    /// Keeps the current choice when it is still allowed, otherwise falls back to the first allowed
    /// value (null when nothing is allowed).
    /// </summary>
    /// <param name="aCurrent">The value currently chosen.</param>
    /// <param name="aAllowed">The values allowed now.</param>
    public static string? KeepOrFirst(string? aCurrent, IReadOnlyList<string> aAllowed)
    {
        if (aCurrent is not null && aAllowed.Contains(aCurrent))
        {
            return aCurrent;
        }

        return aAllowed.Count > 0 ? aAllowed[0] : null;
    }

    /// <summary>
    /// Picks the row for the current state: keeps the current row when still listed, else the first
    /// listed row on the chosen tier, else the first listed row; null when nothing is listed.
    /// </summary>
    /// <param name="aCurrentId">The currently picked row id.</param>
    /// <param name="aListed">The rows the picker lists.</param>
    /// <param name="aTier">The tier button the creator chose.</param>
    public static string? PickModel(string? aCurrentId, IReadOnlyList<CatalogModelResponse> aListed, GenerationTier aTier)
    {
        if (aCurrentId is not null && aListed.Any(aModel => aModel.Id == aCurrentId))
        {
            return aCurrentId;
        }

        return (aListed.FirstOrDefault(aModel => aModel.Tier == aTier) ?? aListed.FirstOrDefault())?.Id;
    }

    /// <summary>
    /// The duration, resolution and aspect values the controls offer for a row (BRD-109). A Venice
    /// row (or any row that states values) offers only its own; an empty list from a row that states
    /// none means the defaults apply, except resolution, which then is not offered at all.
    /// </summary>
    /// <param name="aModel">The picked row, or null.</param>
    public static AllowedVideoValues Allowed(CatalogModelResponse? aModel)
    {
        var vDurations = aModel is { Durations.Length: > 0 } ? aModel.Durations : DefaultDurations;
        var vResolutions = aModel?.Resolutions ?? Array.Empty<string>();
        var vAspects = aModel is { AspectRatios.Length: > 0 } ? aModel.AspectRatios : DefaultAspectRatios;
        return new AllowedVideoValues(vDurations.ToArray(), vResolutions.ToArray(), vAspects.ToArray());
    }

    /// <summary>Parses a duration value such as <c>"5s"</c> or <c>"5"</c> into whole seconds (0 when unreadable).</summary>
    /// <param name="aDuration">The duration value.</param>
    public static int Seconds(string? aDuration)
    {
        var vText = (aDuration ?? string.Empty).Trim().TrimEnd('s', 'S');
        return int.TryParse(vText, out var vSec) ? vSec : 0;
    }

    /// <summary>
    /// Turns a Venice failure (<c>"Venice: &lt;text&gt; (HTTP …)"</c>) into the studio's alert line,
    /// keeping Venice's own text word for word.
    /// </summary>
    /// <param name="aFailure">The job's failure message or the 502 problem detail.</param>
    public static string FailureLine(string? aFailure)
    {
        var vText = (aFailure ?? string.Empty).Trim();
        if (vText.StartsWith("Venice:", StringComparison.OrdinalIgnoreCase))
        {
            vText = vText["Venice:".Length..].Trim();
        }

        return string.IsNullOrEmpty(vText)
            ? "Venice could not make this clip."
            : $"Venice could not make this clip: {vText}";
    }
}

/// <summary>The values the Video studio controls may offer for the picked row.</summary>
/// <param name="Durations">Allowed durations (e.g. <c>"5s"</c>).</param>
/// <param name="Resolutions">Allowed resolutions (e.g. <c>"720p"</c>); empty hides the control.</param>
/// <param name="AspectRatios">Allowed aspect ratios (e.g. <c>"16:9"</c>).</param>
public sealed record AllowedVideoValues(
    IReadOnlyList<string> Durations,
    IReadOnlyList<string> Resolutions,
    IReadOnlyList<string> AspectRatios);
