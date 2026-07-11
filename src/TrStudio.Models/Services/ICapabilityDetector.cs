using TrStudio.Models.Entities;

namespace TrStudio.Models.Services;

/// <summary>
/// Detects the host machine's hardware/OS capabilities for the setup flow and the
/// local-inference readiness decision (TrStudioAdmin hardware detection).
/// </summary>
public interface ICapabilityDetector
{
    /// <summary>
    /// Probes the host and produces a capability report.
    /// </summary>
    /// <param name="aCancellationToken">Token to cancel the probe.</param>
    /// <returns>The detected <see cref="HostCapabilityReport"/>.</returns>
    Task<HostCapabilityReport> DetectAsync(CancellationToken aCancellationToken = default);
}
