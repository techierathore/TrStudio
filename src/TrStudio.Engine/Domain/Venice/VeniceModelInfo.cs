using System.Text.Json;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;

namespace TrStudio.Engine.Domain.Venice;

/// <summary>
/// One entry of Venice's public model list (<c>GET /models?type=…</c>): id, type, the constraints
/// the studios filter on, and the listed US-dollar prices (owner decision 1A — the picture price).
/// </summary>
public sealed class VeniceModelInfo
{
    /// <summary>Venice's model id.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Venice's display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The list type: <c>image</c>, <c>inpaint</c>, <c>upscale</c> or <c>video</c>.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>For video: <c>text-to-video</c>, <c>image-to-video</c> or <c>video</c>.</summary>
    public string? VideoModelType { get; init; }

    /// <summary>Allowed durations (video).</summary>
    public string[] Durations { get; init; } = Array.Empty<string>();

    /// <summary>Allowed resolutions.</summary>
    public string[] Resolutions { get; init; } = Array.Empty<string>();

    /// <summary>Allowed aspect ratios.</summary>
    public string[] AspectRatios { get; init; } = Array.Empty<string>();

    /// <summary>Whether a text-to-image model accepts style reference pictures.</summary>
    public bool SupportsStyleReferences { get; init; }

    /// <summary>The most input pictures Venice states, if any.</summary>
    public int? MaxInputImages { get; init; }

    /// <summary>Flat listed price per picture (<c>pricing.generation.usd</c>).</summary>
    public decimal? GenerationUsd { get; init; }

    /// <summary>Flat listed price per edit (<c>pricing.inpaint.usd</c>).</summary>
    public decimal? InpaintUsd { get; init; }

    /// <summary>Listed price per resolution tier (<c>pricing.resolutions.&lt;tier&gt;.usd</c>).</summary>
    public IReadOnlyDictionary<string, decimal> ResolutionUsd { get; init; } = new Dictionary<string, decimal>();

    /// <summary>Listed upscale price per factor (<c>pricing.upscale.2x.usd</c>).</summary>
    public IReadOnlyDictionary<string, decimal> UpscaleUsd { get; init; } = new Dictionary<string, decimal>();

    /// <summary>
    /// The listed picture price for a call (decision 1A): the chosen resolution's price when the model
    /// prices by resolution, else the flat edit or generation price; the factor's price for upscale.
    /// </summary>
    /// <param name="aTask">The task being run.</param>
    /// <param name="aResolution">The chosen resolution tier, if any.</param>
    /// <param name="aScale">The upscale factor (2 or 4).</param>
    /// <returns>The US-dollar price, or null when the list states none.</returns>
    public decimal? ListedPrice(GenerationTaskType aTask, string? aResolution, int aScale)
    {
        if (aTask == GenerationTaskType.Upscale)
        {
            return UpscaleUsd.TryGetValue($"{aScale}x", out var vUp) ? vUp : GenerationUsd;
        }

        if (ResolutionUsd.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(aResolution))
            {
                var vMatch = ResolutionUsd.FirstOrDefault(aPair => string.Equals(aPair.Key, aResolution, StringComparison.OrdinalIgnoreCase));
                if (vMatch.Key is not null)
                {
                    return vMatch.Value;
                }
            }

            if (Resolutions.Length > 0 && ResolutionUsd.TryGetValue(Resolutions[0], out var vDefault))
            {
                return vDefault;
            }

            return ResolutionUsd.Values.Min();
        }

        return aTask == GenerationTaskType.ImageToImage ? InpaintUsd ?? GenerationUsd : GenerationUsd ?? InpaintUsd;
    }

    /// <summary>
    /// Maps the entry to the "Fill from Venice" answer (decision 4B): the TrStudio task, the two
    /// ability flags and the allowed values. Null for a kind TrStudio never sends to Venice
    /// (video-to-video).
    /// </summary>
    /// <returns>The fill answer, or null when the model's kind is not supported.</returns>
    public VeniceFillResponse? ToFill()
    {
        GenerationTaskType? vTask = Type switch
        {
            "image" => GenerationTaskType.TextToImage,
            "inpaint" => GenerationTaskType.ImageToImage,
            "upscale" => GenerationTaskType.Upscale,
            "video" when VideoModelType == "text-to-video" => GenerationTaskType.TextToVideo,
            "video" when VideoModelType == "image-to-video" => GenerationTaskType.ImageToVideo,
            _ => null
        };

        if (vTask is null)
        {
            return null;
        }

        var vReferenceToVideo = vTask == GenerationTaskType.ImageToVideo
            && Id.Contains("reference-to-video", StringComparison.OrdinalIgnoreCase);

        return new VeniceFillResponse
        {
            ProviderModelId = Id,
            DisplayName = Name,
            Task = vTask.Value,
            TakesReferencePicture = vTask switch
            {
                GenerationTaskType.TextToImage => SupportsStyleReferences,
                GenerationTaskType.TextToVideo => false,
                _ => true
            },
            KeepsFixedCharacter = vReferenceToVideo,
            MaxInputImages = MaxInputImages,
            Durations = Durations,
            Resolutions = Resolutions,
            AspectRatios = AspectRatios,
            ListedPriceUsd = vTask is GenerationTaskType.TextToVideo or GenerationTaskType.ImageToVideo
                ? null
                : ListedPrice(vTask.Value, null, 2)
        };
    }

    /// <summary>
    /// Parses a <c>GET /models</c> response body (<c>{"data":[…]}</c>).
    /// </summary>
    /// <param name="aJson">The response body.</param>
    /// <returns>The parsed entries; entries without an id are skipped.</returns>
    public static IReadOnlyList<VeniceModelInfo> ParseList(string aJson)
    {
        using var vDocument = JsonDocument.Parse(aJson);
        if (!vDocument.RootElement.TryGetProperty("data", out var vData) || vData.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<VeniceModelInfo>();
        }

        var vResult = new List<VeniceModelInfo>();
        foreach (var vItem in vData.EnumerateArray())
        {
            var vId = Str(vItem, "id");
            if (string.IsNullOrWhiteSpace(vId))
            {
                continue;
            }

            var vSpec = Obj(vItem, "model_spec");
            var vConstraints = vSpec is null ? (JsonElement?)null : Obj(vSpec.Value, "constraints");
            var vPricing = vSpec is null ? (JsonElement?)null : Obj(vSpec.Value, "pricing");

            vResult.Add(new VeniceModelInfo
            {
                Id = vId,
                Name = (vSpec is null ? null : Str(vSpec.Value, "name")) ?? vId,
                Type = Str(vItem, "type") ?? string.Empty,
                VideoModelType = vConstraints is null ? null : Str(vConstraints.Value, "model_type"),
                Durations = Strings(vConstraints, "durations"),
                Resolutions = Strings(vConstraints, "resolutions"),
                AspectRatios = Strings(vConstraints, "aspect_ratios") is { Length: > 0 } vSnake ? vSnake : Strings(vConstraints, "aspectRatios"),
                SupportsStyleReferences = vSpec is not null && vSpec.Value.TryGetProperty("supportsStyleReferences", out var vRefs) && vRefs.ValueKind == JsonValueKind.True,
                MaxInputImages = vConstraints is not null && vConstraints.Value.TryGetProperty("maxInputImages", out var vMax) && vMax.ValueKind == JsonValueKind.Number
                    ? vMax.GetInt32()
                    : null,
                GenerationUsd = Usd(vPricing, "generation"),
                InpaintUsd = Usd(vPricing, "inpaint"),
                ResolutionUsd = UsdMap(vPricing, "resolutions"),
                UpscaleUsd = UsdMap(vPricing, "upscale")
            });
        }

        return vResult;
    }

    private static string? Str(JsonElement aElement, string aKey)
        => aElement.ValueKind == JsonValueKind.Object && aElement.TryGetProperty(aKey, out var vValue) && vValue.ValueKind == JsonValueKind.String
            ? vValue.GetString()
            : null;

    private static JsonElement? Obj(JsonElement aElement, string aKey)
        => aElement.ValueKind == JsonValueKind.Object && aElement.TryGetProperty(aKey, out var vValue) && vValue.ValueKind == JsonValueKind.Object
            ? vValue
            : null;

    private static string[] Strings(JsonElement? aElement, string aKey)
    {
        if (aElement is null || !aElement.Value.TryGetProperty(aKey, out var vValue) || vValue.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return vValue.EnumerateArray()
            .Where(aItem => aItem.ValueKind == JsonValueKind.String)
            .Select(aItem => aItem.GetString()!)
            .ToArray();
    }

    private static decimal? Usd(JsonElement? aPricing, string aKey)
    {
        var vBlock = aPricing is null ? null : Obj(aPricing.Value, aKey);
        return vBlock is not null && vBlock.Value.TryGetProperty("usd", out var vUsd) && vUsd.ValueKind == JsonValueKind.Number
            ? vUsd.GetDecimal()
            : null;
    }

    private static IReadOnlyDictionary<string, decimal> UsdMap(JsonElement? aPricing, string aKey)
    {
        var vResult = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var vBlock = aPricing is null ? null : Obj(aPricing.Value, aKey);
        if (vBlock is null)
        {
            return vResult;
        }

        foreach (var vProperty in vBlock.Value.EnumerateObject())
        {
            if (vProperty.Value.ValueKind == JsonValueKind.Object
                && vProperty.Value.TryGetProperty("usd", out var vUsd)
                && vUsd.ValueKind == JsonValueKind.Number)
            {
                vResult[vProperty.Name] = vUsd.GetDecimal();
            }
        }

        return vResult;
    }
}
