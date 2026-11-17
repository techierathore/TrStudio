using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>Data access for digital twins; every call applies the owner filter (BRD-81).</summary>
public interface IDigitalTwinRepository
{
    /// <summary>Inserts a twin.</summary>
    /// <param name="aTwin">The twin.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task InsertAsync(DigitalTwin aTwin, CancellationToken aCancellationToken = default);

    /// <summary>Gets one of the user's twins.</summary>
    /// <param name="aTwinId">The twin.</param>
    /// <param name="aUserId">The owner.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The twin, or null when it is not the user's.</returns>
    Task<DigitalTwin?> GetAsync(Guid aTwinId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>Lists the user's twins, newest first.</summary>
    /// <param name="aUserId">The owner.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The twins.</returns>
    Task<IReadOnlyList<DigitalTwin>> ListAsync(string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>Stores a new Venice voice on a twin.</summary>
    /// <param name="aTwinId">The twin.</param>
    /// <param name="aUserId">The owner.</param>
    /// <param name="aVoiceId">The new voice id.</param>
    /// <param name="aVoiceCreatedUtc">When it was made.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is updated.</returns>
    Task UpdateVoiceAsync(Guid aTwinId, string aUserId, string aVoiceId, DateTime aVoiceCreatedUtc, CancellationToken aCancellationToken = default);
}
