namespace TrStudio.Engine.Domain.Install;

/// <summary>
/// A resumable download request handed to <see cref="IInstallDownloader"/>.
/// </summary>
public class InstallDownloadRequest
{
    /// <summary>The HTTPS URL to download.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The FINAL file path; the downloader stages into <c>{TargetFilePath}.partial</c>.</summary>
    public string TargetFilePath { get; set; } = string.Empty;

    /// <summary>Optional bearer token for authenticated sources; sent as a header, never in the URL.</summary>
    public string? BearerToken { get; set; }
}
