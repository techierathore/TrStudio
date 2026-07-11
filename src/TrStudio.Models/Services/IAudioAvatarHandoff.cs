using TrStudio.Models.Contracts;

namespace TrStudio.Models.Services;

/// <summary>
/// Builds the audio→avatar alignment handoff (FN-035/BRD-26): the voice job output plus timing
/// metadata become the avatar-pipeline job input. Structural only — the avatar pipeline is Phase 5.
/// </summary>
public interface IAudioAvatarHandoff
{
    /// <summary>
    /// Builds the avatar-pipeline handoff contract from a completed voice asset.
    /// </summary>
    /// <param name="aRequest">The handoff request (asset, audio path, duration, language).</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The handoff contract for the Phase-5 avatar pipeline.</returns>
    Task<AvatarHandoffResponse> BuildHandoffAsync(AvatarHandoffRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);
}
