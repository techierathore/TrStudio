using System.Text.Json;
using TrStudio.Models.Entities;

namespace TrStudio.Engine.Domain.Venice;

/// <summary>
/// The job params a Venice call reads (keys of the declarative params schema): <c>prompt</c>,
/// <c>negativePrompt</c>, <c>width</c>, <c>height</c>, <c>resolution</c>, <c>aspectRatio</c>,
/// <c>duration</c>, <c>audio</c>, <c>scale</c>, <c>sourceAssetId</c> / <c>sourceAssetIds</c>,
/// <c>referenceAssetId</c> / <c>referenceAssetIds</c>, <c>characterId</c>, and for reference to video
/// <c>referenceImageAssetIds</c>, <c>referenceVideoAssetIds</c>, <c>referenceAudioAssetIds</c> and <c>endImageAssetId</c>.
/// </summary>
public sealed class VeniceJobParams
{
    /// <summary>The prompt.</summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>The negative prompt, if any.</summary>
    public string? NegativePrompt { get; init; }

    /// <summary>Width in pixels, if given.</summary>
    public int? Width { get; init; }

    /// <summary>Height in pixels, if given.</summary>
    public int? Height { get; init; }

    /// <summary>Resolution tier (e.g. "1K", "480p"), if given.</summary>
    public string? Resolution { get; init; }

    /// <summary>Aspect ratio (e.g. "16:9"), if given.</summary>
    public string? AspectRatio { get; init; }

    /// <summary>Clip duration (e.g. "5s"), if given.</summary>
    public string? Duration { get; init; }

    /// <summary>Whether a clip carries audio, if given.</summary>
    public bool? Audio { get; init; }

    /// <summary>Upscale factor: 2 or 4 (default 2).</summary>
    public int Scale { get; init; } = 2;

    /// <summary>Source pictures (image to image, image to video, upscale), in order.</summary>
    public IReadOnlyList<Guid> SourceAssetIds { get; init; } = Array.Empty<Guid>();

    /// <summary>Reference pictures (style references), in order.</summary>
    public IReadOnlyList<Guid> ReferenceAssetIds { get; init; } = Array.Empty<Guid>();

    /// <summary>The chosen character, for a fixed-character model.</summary>
    public Guid? CharacterId { get; init; }

    /// <summary>Reference clips (reference to video: a twin's clip, or the clip being extended), in order.</summary>
    public IReadOnlyList<Guid> ReferenceVideoAssetIds { get; init; } = Array.Empty<Guid>();

    /// <summary>Reference audio (reference to video: the line spoken in a twin's voice), in order.</summary>
    public IReadOnlyList<Guid> ReferenceAudioAssetIds { get; init; } = Array.Empty<Guid>();

    /// <summary>Reference pictures sent as <c>reference_image_urls</c> on any model (a twin's pictures), in order.</summary>
    public IReadOnlyList<Guid> ReferenceImageAssetIds { get; init; } = Array.Empty<Guid>();

    /// <summary>The picture a clip must end on (<c>end_image_url</c>), if any.</summary>
    public Guid? EndImageAssetId { get; init; }

    /// <summary>Total seconds of the reference clips; Venice bills them on top and its quote needs them.</summary>
    public double? ReferenceVideoSeconds { get; init; }

    /// <summary>
    /// Resolves a value the model constrains: the requested value when given, else the first allowed
    /// value of the params schema entry <paramref name="aKey"/>, else <paramref name="aFallback"/>.
    /// </summary>
    /// <param name="aModel">The registry row.</param>
    /// <param name="aKey">The params schema key (<c>duration</c>, <c>resolution</c>, <c>aspectRatio</c>).</param>
    /// <param name="aRequested">The requested value.</param>
    /// <param name="aFallback">The fallback when nothing is stated.</param>
    /// <returns>The value to send, or null.</returns>
    public static string? Pick(RegistryModel aModel, string aKey, string? aRequested, string? aFallback)
    {
        if (!string.IsNullOrWhiteSpace(aRequested))
        {
            return aRequested;
        }

        var vSpec = aModel.Params.FirstOrDefault(aSpec => string.Equals(aSpec.Key, aKey, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(vSpec?.DefaultValue))
        {
            return vSpec.DefaultValue;
        }

        return vSpec?.Values is { Length: > 0 } vValues ? vValues[0] : aFallback;
    }

    /// <summary>Parses the job's ParamsJson; unknown or malformed values are ignored.</summary>
    /// <param name="aParamsJson">The job params JSON.</param>
    /// <returns>The parsed params.</returns>
    public static VeniceJobParams Parse(string aParamsJson)
    {
        JsonDocument vDocument;
        try
        {
            vDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(aParamsJson) ? "{}" : aParamsJson);
        }
        catch (JsonException)
        {
            vDocument = JsonDocument.Parse("{}");
        }

        using (vDocument)
        {
            var vRoot = vDocument.RootElement;
            if (vRoot.ValueKind != JsonValueKind.Object)
            {
                return new VeniceJobParams();
            }

            var vScale = (int)(Number(vRoot, "scale") ?? 2d);
            return new VeniceJobParams
            {
                Prompt = Text(vRoot, "prompt") ?? string.Empty,
                NegativePrompt = Text(vRoot, "negativePrompt"),
                Width = (int?)Number(vRoot, "width"),
                Height = (int?)Number(vRoot, "height"),
                Resolution = Text(vRoot, "resolution"),
                AspectRatio = Text(vRoot, "aspectRatio"),
                Duration = Text(vRoot, "duration"),
                Audio = vRoot.TryGetProperty("audio", out var vAudio) && vAudio.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? vAudio.GetBoolean()
                    : null,
                Scale = vScale >= 4 ? 4 : 2,
                SourceAssetIds = Ids(vRoot, "sourceAssetId", "sourceAssetIds"),
                ReferenceAssetIds = Ids(vRoot, "referenceAssetId", "referenceAssetIds"),
                CharacterId = Ids(vRoot, "characterId", "characterIds").FirstOrDefault() is var vCharacter && vCharacter != Guid.Empty ? vCharacter : null,
                ReferenceVideoAssetIds = Ids(vRoot, "referenceVideoAssetId", "referenceVideoAssetIds"),
                ReferenceAudioAssetIds = Ids(vRoot, "referenceAudioAssetId", "referenceAudioAssetIds"),
                ReferenceImageAssetIds = Ids(vRoot, "referenceImageAssetId", "referenceImageAssetIds"),
                ReferenceVideoSeconds = Number(vRoot, "referenceVideoSeconds"),
                EndImageAssetId = Ids(vRoot, "endImageAssetId", "endImageAssetIds").FirstOrDefault() is var vEnd && vEnd != Guid.Empty ? vEnd : null
            };
        }
    }

    private static string? Text(JsonElement aRoot, string aKey)
    {
        if (!aRoot.TryGetProperty(aKey, out var vValue))
        {
            return null;
        }

        var vText = vValue.ValueKind switch
        {
            JsonValueKind.String => vValue.GetString(),
            JsonValueKind.Number => vValue.GetRawText(),
            _ => null
        };
        return string.IsNullOrWhiteSpace(vText) ? null : vText;
    }

    private static double? Number(JsonElement aRoot, string aKey)
    {
        if (!aRoot.TryGetProperty(aKey, out var vValue))
        {
            return null;
        }

        return vValue.ValueKind switch
        {
            JsonValueKind.Number => vValue.GetDouble(),
            JsonValueKind.String when double.TryParse(vValue.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var vParsed) => vParsed,
            _ => null
        };
    }

    private static IReadOnlyList<Guid> Ids(JsonElement aRoot, string aSingleKey, string aListKey)
    {
        var vResult = new List<Guid>();
        if (aRoot.TryGetProperty(aSingleKey, out var vSingle) && vSingle.ValueKind == JsonValueKind.String
            && Guid.TryParse(vSingle.GetString(), out var vId))
        {
            vResult.Add(vId);
        }

        if (aRoot.TryGetProperty(aListKey, out var vList) && vList.ValueKind == JsonValueKind.Array)
        {
            foreach (var vItem in vList.EnumerateArray())
            {
                if (vItem.ValueKind == JsonValueKind.String && Guid.TryParse(vItem.GetString(), out var vListId) && !vResult.Contains(vListId))
                {
                    vResult.Add(vListId);
                }
            }
        }

        return vResult;
    }
}
