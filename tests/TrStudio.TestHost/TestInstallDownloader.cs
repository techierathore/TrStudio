using System.Collections.Concurrent;
using TrStudio.Engine.Domain.Install;

namespace TrStudio.TestHost;

/// <summary>
/// TEST ONLY: the test host's model downloader. A download URL the Model Library seed registers
/// (tests/verify/seed/admin-models.sh) never touches the network: it reports the sample's size and
/// progress once, then holds the transfer open until it is paused or cancelled, so the install queue
/// shows a live "downloading" row. Every other URL goes to the real downloader unchanged.
/// </summary>
public sealed class TestInstallDownloader : IInstallDownloader
{
    private readonly IInstallDownloader objReal;
    private readonly ConcurrentDictionary<string, InstallDownloadProgress> objHeld = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the stand-in over the real downloader.</summary>
    /// <param name="aReal">The real downloader for every URL that is not held.</param>
    public TestInstallDownloader(IInstallDownloader aReal)
    {
        objReal = aReal;
    }

    /// <summary>Holds a URL: its download reports this progress and never finishes on its own.</summary>
    /// <param name="aUrl">The download URL.</param>
    /// <param name="aProgress">The size, downloaded bytes and speed it reports.</param>
    public void Hold(string aUrl, InstallDownloadProgress aProgress) => objHeld[aUrl] = aProgress;

    /// <inheritdoc />
    public Task<InstallProbeResult> ProbeAsync(string aUrl, string? aBearerToken, CancellationToken aCancellationToken = default)
    {
        if (objHeld.TryGetValue(aUrl, out var vHeld))
        {
            return Task.FromResult(new InstallProbeResult { ContentLength = vHeld.TotalBytes, SupportsRanges = true });
        }

        return objReal.ProbeAsync(aUrl, aBearerToken, aCancellationToken);
    }

    /// <inheritdoc />
    public async Task<InstallDownloadResult> DownloadAsync(InstallDownloadRequest aRequest, IProgress<InstallDownloadProgress>? aProgress, CancellationToken aCancellationToken = default)
    {
        if (!objHeld.TryGetValue(aRequest.Url, out var vHeld))
        {
            return await objReal.DownloadAsync(aRequest, aProgress, aCancellationToken);
        }

        aProgress?.Report(vHeld);
        await Task.Delay(Timeout.Infinite, aCancellationToken);
        return new InstallDownloadResult { TotalBytes = vHeld.TotalBytes ?? 0, TransferredBytes = vHeld.DownloadedBytes };
    }
}
