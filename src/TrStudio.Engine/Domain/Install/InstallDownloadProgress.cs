namespace TrStudio.Engine.Domain.Install;

/// <summary>
/// A live progress sample emitted while <see cref="IInstallDownloader"/> transfers a file.
/// </summary>
public class InstallDownloadProgress
{
    /// <summary>Bytes on disk so far (including previously-resumed bytes).</summary>
    public long DownloadedBytes { get; set; }

    /// <summary>Total expected bytes, if the server reported a length.</summary>
    public long? TotalBytes { get; set; }

    /// <summary>Current transfer speed in bytes per second.</summary>
    public double SpeedBytesPerSec { get; set; }
}
