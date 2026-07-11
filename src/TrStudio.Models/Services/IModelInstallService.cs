using TrStudio.Models.Entities;

namespace TrStudio.Models.Services;

/// <summary>
/// Manages model downloads/installs from configured model sources, with pause/resume/cancel
/// control for the admin install screen.
/// </summary>
public interface IModelInstallService
{
    /// <summary>
    /// Starts a new model install from a source.
    /// </summary>
    /// <param name="aModelName">The model name/reference to download.</param>
    /// <param name="aSourceId">The model source to download from.</param>
    /// <param name="aDestination">The destination path or volume.</param>
    /// <param name="aCancellationToken">Token to cancel starting the install.</param>
    /// <returns>The tracked install item.</returns>
    Task<ModelInstallItem> StartInstallAsync(string aModelName, Guid aSourceId, string aDestination, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Pauses a running install.
    /// </summary>
    /// <param name="aInstallId">The install identifier.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the install is paused.</returns>
    Task PauseAsync(Guid aInstallId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Resumes a paused install.
    /// </summary>
    /// <param name="aInstallId">The install identifier.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the install is resumed.</returns>
    Task ResumeAsync(Guid aInstallId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Cancels an install and cleans up partial downloads.
    /// </summary>
    /// <param name="aInstallId">The install identifier.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the install is cancelled.</returns>
    Task CancelAsync(Guid aInstallId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists all tracked installs.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>All install items with live progress.</returns>
    Task<IReadOnlyList<ModelInstallItem>> ListAsync(CancellationToken aCancellationToken = default);
}
