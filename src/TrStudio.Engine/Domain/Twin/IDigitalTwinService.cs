using TrStudio.Models.Contracts;

namespace TrStudio.Engine.Domain.Twin;

/// <summary>
/// Digital twins (REQ-FN-041): create, list and make clips of a twin, behind the owner's on/off
/// switch and recorded face and voice consent. Extends the seam multi-shot assembly uses.
/// </summary>
public interface IDigitalTwinService : IDigitalTwinShots
{
    /// <summary>Whether the owner has turned digital twins on (off on a fresh app).</summary>
    /// <param name="aCancellationToken">Token to cancel the read.</param>
    /// <returns>True when on.</returns>
    Task<bool> GetEnabledAsync(CancellationToken aCancellationToken = default);

    /// <summary>Turns digital twins on or off.</summary>
    /// <param name="aEnabled">The new setting.</param>
    /// <param name="aCancellationToken">Token to cancel the write.</param>
    /// <returns>A task completing when the setting is stored.</returns>
    Task SetEnabledAsync(bool aEnabled, CancellationToken aCancellationToken = default);

    /// <summary>Stores one uploaded twin file (picture, clip or voice sample) as the user's asset.</summary>
    /// <param name="aKind"><c>picture</c>, <c>clip</c> or <c>voice</c>.</param>
    /// <param name="aFileName">The file name; its extension names the format.</param>
    /// <param name="aBytes">The file bytes.</param>
    /// <param name="aUserId">The signed-in user.</param>
    /// <param name="aCancellationToken">Token to cancel the write.</param>
    /// <returns>The asset id.</returns>
    /// <exception cref="ArgumentException">The file is empty, too big or the wrong format.</exception>
    Task<Guid> UploadAsync(string aKind, string aFileName, byte[] aBytes, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>Creates a twin: refused when twins are off or consent is missing; otherwise makes the Venice voice and stores the twin.</summary>
    /// <param name="aRequest">The twin's name, person, pictures, clip and voice sample.</param>
    /// <param name="aUserId">The signed-in user.</param>
    /// <param name="aCancellationToken">Token to cancel the creation.</param>
    /// <returns>The stored twin.</returns>
    /// <exception cref="DigitalTwinRefusedException">Twins are off or consent is missing.</exception>
    /// <exception cref="ArgumentException">The request is incomplete or out of range.</exception>
    Task<DigitalTwinResponse> CreateAsync(DigitalTwinCreateRequest aRequest, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>Lists the user's twins, newest first.</summary>
    /// <param name="aUserId">The signed-in user.</param>
    /// <param name="aCancellationToken">Token to cancel the read.</param>
    /// <returns>The twins.</returns>
    Task<IReadOnlyList<DigitalTwinResponse>> ListAsync(string aUserId, CancellationToken aCancellationToken = default);
}
