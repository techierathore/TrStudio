namespace TrStudio.Engine.Domain.Install;

/// <summary>
/// The result of a HEAD probe against a download URL (size/VRAM/licence surfacing, REQ-FN-011).
/// </summary>
public class InstallProbeResult
{
    /// <summary>Content length in bytes, if the server reported one.</summary>
    public long? ContentLength { get; set; }

    /// <summary>Whether the server advertises HTTP Range support (Accept-Ranges: bytes).</summary>
    public bool SupportsRanges { get; set; }
}
