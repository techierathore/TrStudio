namespace TrStudio.Models.Entities;

/// <summary>
/// The hardware/OS capability report produced by the capability detector, driving
/// the setup flow and the local-inference readiness decision.
/// </summary>
public class HostCapabilityReport
{
    /// <summary>GPU core count, if detectable.</summary>
    public int? GpuCoreCount { get; set; }

    /// <summary>Unified memory in gigabytes (Apple Silicon), if applicable.</summary>
    public double? UnifiedMemoryGb { get; set; }

    /// <summary>Whether MPS/CoreML acceleration is available.</summary>
    public bool HasMpsCoreMl { get; set; }

    /// <summary>Logical CPU core count.</summary>
    public int CpuCores { get; set; }

    /// <summary>Total physical memory in gigabytes (all platforms).</summary>
    public double TotalRamGb { get; set; }

    /// <summary>Total disk capacity in gigabytes.</summary>
    public double TotalDiskGb { get; set; }

    /// <summary>Free disk space in gigabytes.</summary>
    public double FreeDiskGb { get; set; }

    /// <summary>Operating system description string.</summary>
    public string OsDescription { get; set; } = string.Empty;

    /// <summary>Overall verdict: whether this host can run local inference.</summary>
    public bool IsReadyForLocalInference { get; set; }

    /// <summary>Warnings surfaced to the admin (e.g. marginal memory, low disk).</summary>
    public string[] Warnings { get; set; } = Array.Empty<string>();
}
