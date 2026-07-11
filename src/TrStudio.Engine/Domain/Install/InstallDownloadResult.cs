namespace TrStudio.Engine.Domain.Install;

/// <summary>
/// The result of a completed <see cref="IInstallDownloader"/> transfer.
/// </summary>
public class InstallDownloadResult
{
    /// <summary>Total bytes of the completed file on disk.</summary>
    public long TotalBytes { get; set; }

    /// <summary>Bytes actually transferred by THIS call (excludes previously-resumed bytes).</summary>
    public long TransferredBytes { get; set; }

    /// <summary>Whether the transfer resumed from a .partial file via an HTTP Range request.</summary>
    public bool WasResumed { get; set; }
}
