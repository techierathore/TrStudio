using TrStudio.Models.Entities;

namespace TrStudio.Engine.Domain.Install;

/// <summary>
/// Pre-install preview (REQ-FN-011): surfaces the size, licence and minimum VRAM of a model
/// BEFORE the admin confirms the install, along with the planned destination.
/// </summary>
public interface IInstallPreviewService
{
    /// <summary>
    /// Probes a model download and returns a preview item (state "preview") without queuing anything.
    /// </summary>
    /// <param name="aModelName">The model name/reference — an absolute URL or a path relative to the source base URL.</param>
    /// <param name="aSourceId">The model source to probe against.</param>
    /// <param name="aCancellationToken">Token to cancel the probe.</param>
    /// <returns>The preview item with size, licence (best-effort) and planned destination.</returns>
    /// <exception cref="InvalidOperationException">The source does not exist.</exception>
    Task<ModelInstallItem> PreviewAsync(string aModelName, Guid aSourceId, CancellationToken aCancellationToken = default);
}
