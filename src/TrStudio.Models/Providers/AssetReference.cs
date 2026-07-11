namespace TrStudio.Models.Providers;

/// <summary>
/// Reference to a completed job's output asset as returned by
/// <see cref="IGenerationProvider.GetResultAsync"/>.
/// </summary>
/// <param name="AssetId">The persisted asset identifier.</param>
/// <param name="StoragePath">Storage path/key of the asset binary.</param>
/// <param name="Kind">The media kind: <c>image</c>, <c>video</c> or <c>audio</c>.</param>
public record AssetReference(
    Guid AssetId,
    string StoragePath,
    string Kind);
