using TrStudio.Models.Contracts;

namespace TrStudio.Models.Services;

/// <summary>
/// The flagship avatar pipeline (FN-037/038/039/040): talking-avatar lip-sync with one-control tier
/// routing, the end-to-end script→video chain, and caption/subtitle export. Every path is
/// owner-scoped (BRD-81) and runs on the proven job spine so the non-disableable safety floors
/// (FN-063) apply to every provider.
/// </summary>
public interface IAvatarPipeline
{
    /// <summary>
    /// Submits a talking-avatar lip-sync job for a character (FN-037). The single tier control swaps
    /// the provider underneath for the same job spec (FN-039); the job degrades cleanly when the
    /// tier's lip-sync model/key is unconfigured.
    /// </summary>
    /// <param name="aRequest">The talking-avatar request.</param>
    /// <param name="aUserId">The owning AppManager user id (BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The submission result with the tier-resolved routing, or null when the character is not found.</returns>
    Task<AvatarTalkingResponse?> SubmitTalkingAvatarAsync(AvatarTalkingRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Runs the end-to-end script→video pipeline for a character (FN-038): script → TTS → lip-sync →
    /// captions. Stages degrade independently on missing models but the chain is real.
    /// </summary>
    /// <param name="aRequest">The script→video request.</param>
    /// <param name="aUserId">The owning AppManager user id (BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The pipeline result, or null when the character is not found or not owned.</returns>
    Task<ScriptToVideoResponse?> RunScriptToVideoAsync(ScriptToVideoRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Generates EN/HI captions/subtitles from a script as a valid, script-timed SRT/VTT sidecar (FN-040).
    /// </summary>
    /// <param name="aRequest">The subtitle request.</param>
    /// <returns>The rendered caption track and cue metadata.</returns>
    SubtitleResponse GenerateSubtitles(SubtitleRequest aRequest);
}
