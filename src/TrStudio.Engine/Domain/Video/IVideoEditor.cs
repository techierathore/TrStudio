using TrStudio.Models.Contracts;

namespace TrStudio.Engine.Domain.Video;

/// <summary>
/// The video edit + export step (FN-054/BRD-45): trim, join, reframe, add audio and export MP4 in a
/// platform aspect format. Runs for real via ffmpeg when it is installed; otherwise degrades cleanly
/// with an actionable message. All asset references (primary clip, joined clips, added audio) are
/// owner-scoped (BRD-81). Returns <c>null</c> when the primary source clip is not found or not owned.
/// </summary>
public interface IVideoEditor
{
    /// <summary>
    /// Applies the requested edit operations and exports the result as MP4.
    /// </summary>
    /// <param name="aRequest">The edit request (source + trim/join/reframe/add-audio + export format).</param>
    /// <param name="aUserId">The owning AppManager user id (owner scoping, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The edit outcome, or null when the primary source clip is not found/owned.</returns>
    Task<VideoEditResponse?> EditAsync(VideoEditRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);
}
