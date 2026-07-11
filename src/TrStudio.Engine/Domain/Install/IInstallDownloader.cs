namespace TrStudio.Engine.Domain.Install;

/// <summary>
/// Resumable HTTPS downloader (REQ-FN-011): writes to <c>{target}.partial</c>, resumes with
/// HTTP Range requests from the partial file's length, and renames into place on completion.
/// </summary>
public interface IInstallDownloader
{
    /// <summary>
    /// Probes a download URL with a HEAD request for its size and range support.
    /// </summary>
    /// <param name="aUrl">The download URL.</param>
    /// <param name="aBearerToken">Optional bearer token for authenticated sources; never logged.</param>
    /// <param name="aCancellationToken">Token to cancel the probe.</param>
    /// <returns>The probe result (content length + range support), best-effort.</returns>
    Task<InstallProbeResult> ProbeAsync(string aUrl, string? aBearerToken, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Downloads (or resumes) a file. Cancellation keeps the .partial file so a later call resumes.
    /// </summary>
    /// <param name="aRequest">The download request (URL, final target path, optional token).</param>
    /// <param name="aProgress">Optional progress sink (bytes, total, speed).</param>
    /// <param name="aCancellationToken">Token cancelling the transfer; the .partial file is kept.</param>
    /// <returns>The completed download result.</returns>
    /// <exception cref="HttpRequestException">The server rejected the request (status carried on the exception).</exception>
    /// <exception cref="OperationCanceledException">The transfer was cancelled; partial state is kept.</exception>
    Task<InstallDownloadResult> DownloadAsync(InstallDownloadRequest aRequest, IProgress<InstallDownloadProgress>? aProgress, CancellationToken aCancellationToken = default);
}
