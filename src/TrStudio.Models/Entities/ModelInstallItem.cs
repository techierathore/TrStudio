namespace TrStudio.Models.Entities;

/// <summary>
/// A model download/install tracked by the model install service, including
/// progress, destination and licence information for the admin install screen.
/// </summary>
public class ModelInstallItem
{
    /// <summary>Unique install identifier.</summary>
    public Guid InstallId { get; set; }

    /// <summary>The model name/reference being installed.</summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>The model source the install downloads from.</summary>
    public Guid SourceId { get; set; }

    /// <summary>Total download size in bytes, if known.</summary>
    public long? TotalBytes { get; set; }

    /// <summary>Bytes downloaded so far.</summary>
    public long DownloadedBytes { get; set; }

    /// <summary>Install state label (e.g. "queued", "downloading", "paused", "completed", "failed").</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>Destination path or volume the model is installed to.</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>Current download speed in bytes per second, if downloading.</summary>
    public double? SpeedBytesPerSec { get; set; }

    /// <summary>The model licence name shown before install (ADR-006), if known.</summary>
    public string? LicenseName { get; set; }

    /// <summary>Minimum GPU VRAM in gigabytes the model needs, if known.</summary>
    public int? MinVramGb { get; set; }

    /// <summary>The resolved download URL the install fetches from (needed to resume across restarts).</summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>Actionable error message when the install failed, if any.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>UTC timestamp when the install was queued.</summary>
    public DateTime CreatedUtc { get; set; }
}
