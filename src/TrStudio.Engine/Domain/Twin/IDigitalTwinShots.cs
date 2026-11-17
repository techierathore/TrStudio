namespace TrStudio.Engine.Domain.Twin;

/// <summary>
/// What multi-shot assembly (REQ-FN-042) needs from a digital twin (REQ-FN-041): to check the twin
/// may be used now, and to queue one twin clip. Implemented by the twin service; the sequence runner
/// only sees this seam.
/// </summary>
public interface IDigitalTwinShots
{
    /// <summary>
    /// Refuses with a plain reason when the twin cannot be used now (twins are off, the twin is not
    /// this user's, or the person's consent is missing or withdrawn); null when it can.
    /// </summary>
    /// <param name="aTwinId">The twin.</param>
    /// <param name="aUserId">The signed-in user.</param>
    /// <param name="aCancellationToken">Token to cancel the check.</param>
    /// <returns>The refusal, or null.</returns>
    Task<string?> RefusalAsync(Guid aTwinId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Queues one twin clip of a line (Wan 3.0 reference to video with the twin's pictures, clip and the
    /// line spoken in its voice). When <paramref name="aStartFrameAssetId"/> is given it is sent as one
    /// more reference picture and the prompt asks the clip to open on it (shot continuity).
    /// </summary>
    /// <param name="aTwinId">The twin.</param>
    /// <param name="aUserId">The signed-in user.</param>
    /// <param name="aLine">The words the person says.</param>
    /// <param name="aStartFrameAssetId">The picture the clip should open on, if any.</param>
    /// <param name="aDuration">Venice duration (e.g. "2s", "5s"); null for the row's default.</param>
    /// <param name="aResolution">Venice resolution (e.g. "480p"); null for the row's default.</param>
    /// <param name="aCancellationToken">Token to cancel the queueing.</param>
    /// <returns>The generation job id.</returns>
    Task<Guid> QueueClipAsync(Guid aTwinId, string aUserId, string aLine, Guid? aStartFrameAssetId, string? aDuration, string? aResolution, CancellationToken aCancellationToken = default);
}
