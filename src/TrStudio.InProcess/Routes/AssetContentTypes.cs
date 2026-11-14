// Ported from src/TrStudioApi/Endpoints/AssetContentTypes.cs (phase 3, ADR-015): the same routes, answered in process.
using Microsoft.Extensions.DependencyInjection;
using TrStudio.InProcess.Hosting;

namespace TrStudio.InProcess.Routes;

/// <summary>
/// Maps a stored asset's file extension to the media type it is served with.
/// </summary>
public static class AssetContentTypes
{
    /// <summary>
    /// Returns the media type for a stored asset path; unknown extensions are served as binary.
    /// </summary>
    /// <param name="aPath">The asset's storage path.</param>
    /// <returns>The media type, e.g. <c>image/png</c>.</returns>
    public static string FromPath(string aPath)
    {
        var vExtension = Path.GetExtension(aPath).ToLowerInvariant();
        return vExtension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            _ => "application/octet-stream"
        };
    }
}
