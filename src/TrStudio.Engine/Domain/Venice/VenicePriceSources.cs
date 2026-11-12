using TrStudio.Models.Enums;

namespace TrStudio.Engine.Domain.Venice;

/// <summary>
/// The <c>GenerationLog.PriceSource</c> labels for Venice's own stated price (owner decision 1A,
/// REQ-FN-082): a video's quote taken just before queueing, or a picture's listed price read at
/// call time.
/// </summary>
public static class VenicePriceSources
{
    /// <summary>The <c>/video/quote</c> answer.</summary>
    public const string Quote = "venice-quote";

    /// <summary>The listed price from <c>GET /models</c>.</summary>
    public const string List = "venice-list";

    /// <summary>Whether a task is a clip (priced by quote).</summary>
    /// <param name="aTask">The task.</param>
    /// <returns>True for text to video and image to video.</returns>
    public static bool IsVideo(GenerationTaskType aTask)
        => aTask is GenerationTaskType.TextToVideo or GenerationTaskType.ImageToVideo;

    /// <summary>The price source for a run on a given provider and task.</summary>
    /// <param name="aProviderKind">The registry row's provider kind.</param>
    /// <param name="aTask">The task.</param>
    /// <returns>The label, or null when the provider does not state its own price.</returns>
    public static string? For(ProviderKind aProviderKind, GenerationTaskType aTask)
        => aProviderKind != ProviderKind.Venice ? null : IsVideo(aTask) ? Quote : List;

    /// <summary>The Venice model-list type a task's model lives in.</summary>
    /// <param name="aTask">The task.</param>
    /// <returns><c>image</c>, <c>inpaint</c>, <c>upscale</c> or <c>video</c>.</returns>
    public static string ListTypeFor(GenerationTaskType aTask) => aTask switch
    {
        GenerationTaskType.ImageToImage => "inpaint",
        GenerationTaskType.Upscale => "upscale",
        GenerationTaskType.TextToVideo or GenerationTaskType.ImageToVideo => "video",
        _ => "image"
    };
}
