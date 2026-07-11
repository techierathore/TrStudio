using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Setup;
using TrStudio.Models.Entities;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Infrastructure.Capability;

/// <summary>
/// Cross-platform <see cref="ICapabilityDetector"/> (REQ-FN-008). On Apple Silicon it parses
/// <c>sysctl</c>/<c>system_profiler</c> for GPU cores, unified memory and MPS/CoreML presence;
/// on Linux/Windows the Apple-specific fields stay null/false and the readiness verdict derives
/// from CPU cores, RAM and free disk. Results are cached in-process with a short TTL.
/// </summary>
public class CapabilityDetector : ICapabilityDetector
{
    private const string CacheKey = "TrStudio.Setup.CapabilityReport";
    private const double BytesPerGb = 1024d * 1024d * 1024d;

    private readonly IMemoryCache objCache;
    private readonly SetupOptions objOptions;
    private readonly SetupPaths objPaths;
    private readonly ILogger<CapabilityDetector> objLogger;

    /// <summary>
    /// Initialises the detector.
    /// </summary>
    /// <param name="aCache">In-process cache holding the report for the configured TTL.</param>
    /// <param name="aOptions">Setup options carrying readiness thresholds and the cache TTL.</param>
    /// <param name="aPaths">Managed-environment paths (the disk probe targets the managed root's drive).</param>
    /// <param name="aLogger">Logger for probe diagnostics.</param>
    public CapabilityDetector(IMemoryCache aCache, SetupOptions aOptions, SetupPaths aPaths, ILogger<CapabilityDetector> aLogger)
    {
        objCache = aCache;
        objOptions = aOptions;
        objPaths = aPaths;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<HostCapabilityReport> DetectAsync(CancellationToken aCancellationToken = default)
    {
        if (objCache.TryGetValue(CacheKey, out HostCapabilityReport? vCached) && vCached is not null)
        {
            return vCached;
        }

        var vReport = await ProbeAsync(aCancellationToken).ConfigureAwait(false);
        objCache.Set(CacheKey, vReport, TimeSpan.FromSeconds(Math.Max(1, objOptions.CapabilityCacheSeconds)));
        return vReport;
    }

    private async Task<HostCapabilityReport> ProbeAsync(CancellationToken aCancellationToken)
    {
        var vWarnings = new List<string>();
        var vReport = new HostCapabilityReport
        {
            CpuCores = Environment.ProcessorCount,
            TotalRamGb = Math.Round(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / BytesPerGb, 1),
            OsDescription = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})"
        };

        ProbeDisk(vReport, vWarnings);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            await ProbeAppleSiliconAsync(vReport, vWarnings, aCancellationToken).ConfigureAwait(false);
        }
        else
        {
            vWarnings.Add("No Apple MPS/CoreML acceleration on this platform — Fast-tier local inference will run on CPU.");
        }

        ApplyReadinessVerdict(vReport, vWarnings);
        vReport.Warnings = vWarnings.ToArray();
        return vReport;
    }

    private void ProbeDisk(HostCapabilityReport aReport, List<string> aWarnings)
    {
        try
        {
            var vProbePath = Directory.Exists(objPaths.Root) ? objPaths.Root : Path.GetPathRoot(objPaths.Root) ?? "/";
            var vDrive = new DriveInfo(vProbePath);
            aReport.TotalDiskGb = Math.Round(vDrive.TotalSize / BytesPerGb, 1);
            aReport.FreeDiskGb = Math.Round(vDrive.AvailableFreeSpace / BytesPerGb, 1);
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "Disk probe failed for managed root {Root}", objPaths.Root);
            aWarnings.Add("Could not measure disk space for the managed environment drive.");
        }
    }

    private async Task ProbeAppleSiliconAsync(HostCapabilityReport aReport, List<string> aWarnings, CancellationToken aCancellationToken)
    {
        var vIsAppleSilicon = RuntimeInformation.OSArchitecture == Architecture.Arm64;
        aReport.HasMpsCoreMl = vIsAppleSilicon;

        var vMemSize = await RunProbeAsync("sysctl", "-n hw.memsize", aCancellationToken).ConfigureAwait(false);
        if (long.TryParse(vMemSize?.Trim(), out var vBytes) && vIsAppleSilicon)
        {
            aReport.UnifiedMemoryGb = Math.Round(vBytes / BytesPerGb, 1);
        }

        var vDisplays = await RunProbeAsync("system_profiler", "SPDisplaysDataType", aCancellationToken).ConfigureAwait(false);
        aReport.GpuCoreCount = ParseGpuCores(vDisplays);
        if (vIsAppleSilicon && aReport.GpuCoreCount is null)
        {
            aWarnings.Add("Apple GPU core count could not be parsed from system_profiler output.");
        }
    }

    /// <summary>
    /// Parses "Total Number of Cores: N" from <c>system_profiler SPDisplaysDataType</c> output.
    /// </summary>
    /// <param name="aSystemProfilerOutput">The raw system_profiler output, or null when the probe failed.</param>
    /// <returns>The GPU core count, or null when not present.</returns>
    public static int? ParseGpuCores(string? aSystemProfilerOutput)
    {
        if (string.IsNullOrWhiteSpace(aSystemProfilerOutput))
        {
            return null;
        }

        foreach (var vLine in aSystemProfilerOutput.Split('\n'))
        {
            var vTrimmed = vLine.Trim();
            if (!vTrimmed.StartsWith("Total Number of Cores:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var vValue = vTrimmed[(vTrimmed.IndexOf(':') + 1)..].Trim();
            if (int.TryParse(vValue, out var vCores))
            {
                return vCores;
            }
        }

        return null;
    }

    private void ApplyReadinessVerdict(HostCapabilityReport aReport, List<string> aWarnings)
    {
        if (aReport.TotalRamGb < objOptions.MinRamGb)
        {
            aWarnings.Add($"Only {aReport.TotalRamGb} GB RAM detected — at least {objOptions.MinRamGb} GB is recommended for local inference.");
        }

        if (aReport.FreeDiskGb < objOptions.MinFreeDiskGb)
        {
            aWarnings.Add($"Only {aReport.FreeDiskGb} GB free disk — at least {objOptions.MinFreeDiskGb} GB is needed for models and caches.");
        }

        aReport.IsReadyForLocalInference =
            aReport.CpuCores >= objOptions.MinCpuCores
            && aReport.TotalRamGb >= objOptions.MinRamGb
            && aReport.FreeDiskGb >= objOptions.MinFreeDiskGb;
    }

    private async Task<string?> RunProbeAsync(string aFileName, string aArguments, CancellationToken aCancellationToken)
    {
        try
        {
            var vStartInfo = new ProcessStartInfo(aFileName, aArguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using var vProcess = Process.Start(vStartInfo);
            if (vProcess is null)
            {
                return null;
            }

            var vOutput = await vProcess.StandardOutput.ReadToEndAsync(aCancellationToken).ConfigureAwait(false);
            await vProcess.WaitForExitAsync(aCancellationToken).ConfigureAwait(false);
            return vProcess.ExitCode == 0 ? vOutput : null;
        }
        catch (Exception vException)
        {
            objLogger.LogDebug(vException, "Capability probe {FileName} {Arguments} unavailable", aFileName, aArguments);
            return null;
        }
    }
}
