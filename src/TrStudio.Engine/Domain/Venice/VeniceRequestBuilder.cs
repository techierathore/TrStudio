using System.Text.Json.Nodes;
using TrStudio.Models.Entities;

namespace TrStudio.Engine.Domain.Venice;

/// <summary>
/// Builds Venice request bodies from a registry row and the job params (pure; unit-tested). Picture
/// calls carry <c>safe_mode</c> = true unless the owner's unrestricted mode is on (BRD-116). The
/// Venice model id comes from the row's <see cref="RegistryModel.ProviderModelId"/>, never from code.
/// </summary>
public static class VeniceRequestBuilder
{
    /// <summary>Venice's seed range is ±999,999,999; a job seed is folded into it deterministically.</summary>
    public const long MaxSeed = 999_999_999;

    /// <summary>Body for <c>POST /image/generate</c>.</summary>
    /// <param name="aModel">The registry row.</param>
    /// <param name="aParams">The job params.</param>
    /// <param name="aSeed">The job seed.</param>
    /// <param name="aSafeMode">The safe-mode flag.</param>
    /// <param name="aStyleReferences">Style reference pictures as data URLs (sent only when the row takes a reference picture).</param>
    /// <returns>The JSON body.</returns>
    public static JsonObject TextToImage(RegistryModel aModel, VeniceJobParams aParams, long aSeed, bool aSafeMode, IReadOnlyList<string> aStyleReferences)
    {
        var vBody = new JsonObject
        {
            ["model"] = ModelId(aModel),
            ["prompt"] = aParams.Prompt,
            ["safe_mode"] = aSafeMode,
            ["seed"] = FoldSeed(aSeed),
            ["format"] = "png",
            ["variants"] = 1,
            ["return_binary"] = false
        };

        AddIfSet(vBody, "negative_prompt", aParams.NegativePrompt);
        AddIfSet(vBody, "resolution", aParams.Resolution);
        AddIfSet(vBody, "aspect_ratio", aParams.AspectRatio);
        if (aParams.Width is > 0 && aParams.Height is > 0 && aParams.AspectRatio is null && aParams.Resolution is null)
        {
            vBody["width"] = aParams.Width.Value;
            vBody["height"] = aParams.Height.Value;
        }

        if (aModel.TakesReferencePicture && aStyleReferences.Count > 0)
        {
            var vRefs = new JsonArray();
            foreach (var vRef in aStyleReferences)
            {
                vRefs.Add(new JsonObject { ["image"] = vRef });
            }

            vBody["style_references"] = vRefs;
        }

        return vBody;
    }

    /// <summary>Body for <c>POST /image/edit</c> (one source) or <c>/image/multi-edit</c> (several).</summary>
    /// <param name="aModel">The registry row.</param>
    /// <param name="aParams">The job params.</param>
    /// <param name="aSafeMode">The safe-mode flag.</param>
    /// <param name="aImages">The source pictures as data URLs (at least one).</param>
    /// <returns>The JSON body and whether it goes to <c>/image/multi-edit</c>.</returns>
    public static (JsonObject Body, bool Multi) ImageToImage(RegistryModel aModel, VeniceJobParams aParams, bool aSafeMode, IReadOnlyList<string> aImages)
    {
        if (aImages.Count == 0)
        {
            throw new InvalidOperationException("Image to image needs a source picture (sourceAssetId).");
        }

        var vMax = aModel.MaxInputImages is > 0 ? aModel.MaxInputImages.Value : int.MaxValue;
        var vImages = aImages.Take(vMax).ToList();
        var vMulti = vImages.Count > 1;
        var vBody = new JsonObject
        {
            ["prompt"] = aParams.Prompt,
            ["safe_mode"] = aSafeMode
        };

        if (vMulti)
        {
            vBody["modelId"] = ModelId(aModel);
            var vArray = new JsonArray();
            foreach (var vImage in vImages)
            {
                vArray.Add(vImage);
            }

            vBody["images"] = vArray;
        }
        else
        {
            vBody["model"] = ModelId(aModel);
            vBody["image"] = vImages[0];
        }

        AddIfSet(vBody, "aspect_ratio", aParams.AspectRatio);
        AddIfSet(vBody, "resolution", aParams.Resolution);
        return (vBody, vMulti);
    }

    /// <summary>Body for <c>POST /image/upscale</c>.</summary>
    /// <param name="aParams">The job params (scale 2 or 4).</param>
    /// <param name="aImageBase64">The source picture as raw base64.</param>
    /// <returns>The JSON body.</returns>
    public static JsonObject Upscale(VeniceJobParams aParams, string aImageBase64)
        => new() { ["image"] = aImageBase64, ["scale"] = aParams.Scale };

    /// <summary>Body for <c>POST /video/quote</c>: the same model, duration, resolution and aspect as the queue call.</summary>
    /// <param name="aModel">The registry row.</param>
    /// <param name="aParams">The job params.</param>
    /// <returns>The JSON body.</returns>
    public static JsonObject VideoQuote(RegistryModel aModel, VeniceJobParams aParams)
    {
        var vBody = new JsonObject
        {
            ["model"] = ModelId(aModel),
            ["duration"] = VeniceJobParams.Pick(aModel, "duration", aParams.Duration, "5s")
        };
        AddIfSet(vBody, "resolution", VeniceJobParams.Pick(aModel, "resolution", aParams.Resolution, null));
        AddIfSet(vBody, "aspect_ratio", VeniceJobParams.Pick(aModel, "aspectRatio", aParams.AspectRatio, null));
        if (aParams.Audio is not null)
        {
            vBody["audio"] = aParams.Audio.Value;
        }

        if (aParams.ReferenceVideoSeconds is > 0)
        {
            vBody["reference_video_total_duration"] = Math.Round(aParams.ReferenceVideoSeconds.Value, 2);
        }

        return vBody;
    }

    /// <summary>Body for <c>POST /video/queue</c>.</summary>
    /// <param name="aModel">The registry row.</param>
    /// <param name="aParams">The job params.</param>
    /// <param name="aImageUrl">The start picture as a data URL (image to video), if any.</param>
    /// <param name="aReferenceImageUrls">The character's reference set as data URLs (fixed character), if any.</param>
    /// <returns>The JSON body.</returns>
    public static JsonObject VideoQueue(RegistryModel aModel, VeniceJobParams aParams, string? aImageUrl, IReadOnlyList<string> aReferenceImageUrls)
        => VideoQueue(aModel, aParams, aImageUrl, aReferenceImageUrls, VideoReferences.None);

    /// <summary>
    /// Body for <c>POST /video/queue</c> with reference media (reference to video, Wan 3.0: up to 10
    /// pictures, 5 clips and 5 audio clips; Venice's Wan 3.0 guide) and an end picture.
    /// </summary>
    /// <param name="aModel">The registry row.</param>
    /// <param name="aParams">The job params.</param>
    /// <param name="aImageUrl">The start picture as a data URL (image to video), if any.</param>
    /// <param name="aReferenceImageUrls">The character's reference set as data URLs (fixed character), if any.</param>
    /// <param name="aReferences">Explicit reference pictures, clips, audio and end picture as data URLs.</param>
    /// <returns>The JSON body.</returns>
    public static JsonObject VideoQueue(RegistryModel aModel, VeniceJobParams aParams, string? aImageUrl, IReadOnlyList<string> aReferenceImageUrls, VideoReferences aReferences)
    {
        var vBody = VideoQuote(aModel, aParams);
        // The quote needs the reference clips' length; the queue call refuses that key (Venice, 17 Nov 2026).
        vBody.Remove("reference_video_total_duration");
        vBody["prompt"] = aParams.Prompt;
        AddIfSet(vBody, "negative_prompt", aParams.NegativePrompt);
        AddIfSet(vBody, "image_url", aImageUrl);
        AddIfSet(vBody, "end_image_url", aReferences.EndImageUrl);
        var vImages = new List<string>(aReferences.ImageUrls);
        if (aModel.KeepsFixedCharacter)
        {
            vImages.AddRange(aReferenceImageUrls);
        }

        AddArray(vBody, "reference_image_urls", vImages, MaxReferenceImages);
        AddArray(vBody, "reference_video_urls", aReferences.VideoUrls, MaxReferenceVideos);
        AddArray(vBody, "reference_audio_urls", aReferences.AudioUrls, MaxReferenceAudio);
        return vBody;
    }

    /// <summary>Wan 3.0 reference to video takes at most 10 pictures.</summary>
    public const int MaxReferenceImages = 10;

    /// <summary>Wan 3.0 reference to video takes at most 5 clips (15 s in all).</summary>
    public const int MaxReferenceVideos = 5;

    /// <summary>Wan 3.0 reference to video takes at most 5 audio clips (15 s in all).</summary>
    public const int MaxReferenceAudio = 5;

    /// <summary>A data URL for a clip or a sound.</summary>
    /// <param name="aBytes">The bytes.</param>
    /// <param name="aFileName">The stored file name (its extension picks the media type).</param>
    /// <returns><c>data:video/…;base64,…</c> or <c>data:audio/…;base64,…</c>.</returns>
    public static string ToMediaDataUrl(byte[] aBytes, string aFileName)
    {
        var vType = Path.GetExtension(aFileName).ToLowerInvariant() switch
        {
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            _ => null
        };
        return vType is null ? ToDataUrl(aBytes, aFileName) : $"data:{vType};base64,{Convert.ToBase64String(aBytes)}";
    }

    private static void AddArray(JsonObject aBody, string aKey, IReadOnlyList<string> aValues, int aMax)
    {
        if (aValues.Count == 0)
        {
            return;
        }

        var vArray = new JsonArray();
        foreach (var vValue in aValues.Take(aMax))
        {
            vArray.Add(vValue);
        }

        aBody[aKey] = vArray;
    }

    /// <summary>Folds a job seed into Venice's accepted range.</summary>
    /// <param name="aSeed">The job seed.</param>
    /// <returns>A seed within ±<see cref="MaxSeed"/>.</returns>
    public static long FoldSeed(long aSeed) => Math.Abs(aSeed % MaxSeed);

    /// <summary>A data URL for a picture's bytes.</summary>
    /// <param name="aBytes">The picture bytes.</param>
    /// <param name="aFileName">The stored file name (its extension picks the media type).</param>
    /// <returns><c>data:image/…;base64,…</c>.</returns>
    public static string ToDataUrl(byte[] aBytes, string aFileName)
    {
        var vType = Path.GetExtension(aFileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/png"
        };
        return $"data:{vType};base64,{Convert.ToBase64String(aBytes)}";
    }

    private static string ModelId(RegistryModel aModel)
        => string.IsNullOrWhiteSpace(aModel.ProviderModelId)
            ? throw new InvalidOperationException($"Registry row '{aModel.Id}' has no Venice model id; set it on Registry & providers.")
            : aModel.ProviderModelId.Trim();

    private static void AddIfSet(JsonObject aBody, string aKey, string? aValue)
    {
        if (!string.IsNullOrWhiteSpace(aValue))
        {
            aBody[aKey] = aValue;
        }
    }
}

/// <summary>Reference media for one clip, as data URLs.</summary>
/// <param name="ImageUrls">Reference pictures (<c>reference_image_urls</c>).</param>
/// <param name="VideoUrls">Reference clips (<c>reference_video_urls</c>).</param>
/// <param name="AudioUrls">Reference audio (<c>reference_audio_urls</c>).</param>
/// <param name="EndImageUrl">The picture the clip ends on (<c>end_image_url</c>).</param>
public sealed record VideoReferences(IReadOnlyList<string> ImageUrls, IReadOnlyList<string> VideoUrls, IReadOnlyList<string> AudioUrls, string? EndImageUrl)
{
    /// <summary>No reference media.</summary>
    public static VideoReferences None { get; } = new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), null);
}
