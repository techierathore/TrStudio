using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Install;

namespace TrStudio.Engine.Workers.Install;

/// <summary>
/// Resumable HTTPS <see cref="IInstallDownloader"/> (REQ-FN-011): stages into
/// <c>{target}.partial</c>, resumes with <c>Range: bytes={existing}-</c> when a partial file
/// exists, restarts cleanly when the server ignores the range (HTTP 200), and renames the
/// partial into place on completion. Auth tokens travel as headers, never in URLs (NFR-05).
/// </summary>
public class InstallDownloader : IInstallDownloader
{
    /// <summary>The named <c>HttpClient</c> this downloader resolves (infinite timeout for large files).</summary>
    public const string HttpClientName = "TrStudio.Setup.Downloads";

    private const int CopyBufferSize = 81920;
    private const int ProgressIntervalMs = 500;

    private readonly IHttpClientFactory objHttpClientFactory;
    private readonly ILogger<InstallDownloader> objLogger;

    /// <summary>
    /// Initialises the downloader.
    /// </summary>
    /// <param name="aHttpClientFactory">Factory resolving the named download client.</param>
    /// <param name="aLogger">Logger for transfer diagnostics (URLs are logged, tokens never).</param>
    public InstallDownloader(IHttpClientFactory aHttpClientFactory, ILogger<InstallDownloader> aLogger)
    {
        objHttpClientFactory = aHttpClientFactory;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<InstallProbeResult> ProbeAsync(string aUrl, string? aBearerToken, CancellationToken aCancellationToken = default)
    {
        var vClient = objHttpClientFactory.CreateClient(HttpClientName);
        using var vRequest = new HttpRequestMessage(HttpMethod.Head, aUrl);
        ApplyToken(vRequest, aBearerToken);

        using var vResponse = await vClient.SendAsync(vRequest, HttpCompletionOption.ResponseHeadersRead, aCancellationToken).ConfigureAwait(false);
        if (vResponse.IsSuccessStatusCode)
        {
            return new InstallProbeResult
            {
                ContentLength = vResponse.Content.Headers.ContentLength,
                SupportsRanges = vResponse.Headers.AcceptRanges.Contains("bytes")
            };
        }

        return await ProbeViaRangeGetAsync(vClient, aUrl, aBearerToken, aCancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<InstallDownloadResult> DownloadAsync(InstallDownloadRequest aRequest, IProgress<InstallDownloadProgress>? aProgress, CancellationToken aCancellationToken = default)
    {
        var vPartialPath = aRequest.TargetFilePath + ".partial";
        Directory.CreateDirectory(Path.GetDirectoryName(aRequest.TargetFilePath)!);
        var vExistingBytes = File.Exists(vPartialPath) ? new FileInfo(vPartialPath).Length : 0L;

        var vClient = objHttpClientFactory.CreateClient(HttpClientName);
        using var vRequest = new HttpRequestMessage(HttpMethod.Get, aRequest.Url);
        ApplyToken(vRequest, aRequest.BearerToken);
        if (vExistingBytes > 0)
        {
            vRequest.Headers.Range = new RangeHeaderValue(vExistingBytes, null);
        }

        using var vResponse = await vClient.SendAsync(vRequest, HttpCompletionOption.ResponseHeadersRead, aCancellationToken).ConfigureAwait(false);
        if (vResponse.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && vExistingBytes > 0)
        {
            // The partial file already covers the full length — finalize it.
            File.Move(vPartialPath, aRequest.TargetFilePath, overwrite: true);
            return new InstallDownloadResult { TotalBytes = vExistingBytes, TransferredBytes = 0, WasResumed = true };
        }

        vResponse.EnsureSuccessStatusCode();

        var vWasResumed = vResponse.StatusCode == HttpStatusCode.PartialContent && vExistingBytes > 0;
        if (!vWasResumed && vExistingBytes > 0)
        {
            objLogger.LogInformation("Server ignored the range request for {Url}; restarting from zero", aRequest.Url);
            File.Delete(vPartialPath);
            vExistingBytes = 0;
        }

        var vTotalBytes = ResolveTotalBytes(vResponse, vExistingBytes);
        var vTransferred = await CopyToPartialAsync(vResponse, vPartialPath, vExistingBytes, vTotalBytes, aProgress, aCancellationToken).ConfigureAwait(false);

        File.Move(vPartialPath, aRequest.TargetFilePath, overwrite: true);
        return new InstallDownloadResult
        {
            TotalBytes = vExistingBytes + vTransferred,
            TransferredBytes = vTransferred,
            WasResumed = vWasResumed
        };
    }

    private static async Task<long> CopyToPartialAsync(
        HttpResponseMessage aResponse,
        string aPartialPath,
        long aExistingBytes,
        long? aTotalBytes,
        IProgress<InstallDownloadProgress>? aProgress,
        CancellationToken aCancellationToken)
    {
        await using var vSource = await aResponse.Content.ReadAsStreamAsync(aCancellationToken).ConfigureAwait(false);
        await using var vTarget = new FileStream(aPartialPath, aExistingBytes > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize, useAsync: true);

        var vBuffer = new byte[CopyBufferSize];
        long vTransferred = 0;
        long vBytesAtLastSample = 0;
        var vStopwatch = Stopwatch.StartNew();
        var vLastSampleMs = 0L;

        while (true)
        {
            var vRead = await vSource.ReadAsync(vBuffer, aCancellationToken).ConfigureAwait(false);
            if (vRead == 0)
            {
                break;
            }

            await vTarget.WriteAsync(vBuffer.AsMemory(0, vRead), aCancellationToken).ConfigureAwait(false);
            vTransferred += vRead;

            var vElapsedMs = vStopwatch.ElapsedMilliseconds;
            if (aProgress is not null && vElapsedMs - vLastSampleMs >= ProgressIntervalMs)
            {
                var vSpeed = (vTransferred - vBytesAtLastSample) * 1000d / Math.Max(1, vElapsedMs - vLastSampleMs);
                aProgress.Report(new InstallDownloadProgress
                {
                    DownloadedBytes = aExistingBytes + vTransferred,
                    TotalBytes = aTotalBytes,
                    SpeedBytesPerSec = vSpeed
                });
                vLastSampleMs = vElapsedMs;
                vBytesAtLastSample = vTransferred;
            }
        }

        return vTransferred;
    }

    private static long? ResolveTotalBytes(HttpResponseMessage aResponse, long aExistingBytes)
    {
        var vContentRangeTotal = aResponse.Content.Headers.ContentRange?.Length;
        if (vContentRangeTotal.HasValue)
        {
            return vContentRangeTotal;
        }

        var vContentLength = aResponse.Content.Headers.ContentLength;
        if (!vContentLength.HasValue)
        {
            return null;
        }

        return aResponse.StatusCode == HttpStatusCode.PartialContent
            ? aExistingBytes + vContentLength.Value
            : vContentLength.Value;
    }

    private async Task<InstallProbeResult> ProbeViaRangeGetAsync(HttpClient aClient, string aUrl, string? aBearerToken, CancellationToken aCancellationToken)
    {
        using var vRequest = new HttpRequestMessage(HttpMethod.Get, aUrl);
        ApplyToken(vRequest, aBearerToken);
        vRequest.Headers.Range = new RangeHeaderValue(0, 0);

        using var vResponse = await aClient.SendAsync(vRequest, HttpCompletionOption.ResponseHeadersRead, aCancellationToken).ConfigureAwait(false);
        vResponse.EnsureSuccessStatusCode();
        return new InstallProbeResult
        {
            ContentLength = vResponse.Content.Headers.ContentRange?.Length ?? vResponse.Content.Headers.ContentLength,
            SupportsRanges = vResponse.StatusCode == HttpStatusCode.PartialContent
        };
    }

    private static void ApplyToken(HttpRequestMessage aRequest, string? aBearerToken)
    {
        if (!string.IsNullOrWhiteSpace(aBearerToken))
        {
            aRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aBearerToken);
        }
    }
}
