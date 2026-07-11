using Microsoft.Extensions.Configuration;

namespace TrStudio.Engine.Domain.Setup;

/// <summary>
/// Configuration for the setup cluster (section <c>Setup</c>): the managed environment root
/// (REQ-FN-010 — the ONLY path provisioning may touch), download pins for the isolated Python
/// and ComfyUI, install destination rules and capability thresholds. Every value has a safe
/// default so the section is optional.
/// </summary>
public class SetupOptions
{
    /// <summary>Config key threshold: downloads larger than this default to the network volume (10 GB).</summary>
    public const long NetworkVolumeThresholdBytes = 10L * 1024 * 1024 * 1024;

    /// <summary>Root of the managed environment; <c>~</c> expands to the user profile cross-platform.</summary>
    public string ManagedEnvRoot { get; set; } = Path.Combine("~", ".trstudio", "env");

    /// <summary>RunPod network-volume path large model downloads default to (may be a placeholder until RunPod is wired).</summary>
    public string NetworkVolumeRoot { get; set; } = "/runpod-volume/models";

    /// <summary>Base URL of the python-build-standalone GitHub release assets.</summary>
    public string PythonStandaloneBaseUrl { get; set; } = "https://github.com/astral-sh/python-build-standalone/releases/download";

    /// <summary>Pinned python-build-standalone release tag.</summary>
    public string PythonStandaloneTag { get; set; } = "20250818";

    /// <summary>Pinned CPython version provisioned into the managed environment.</summary>
    public string PythonVersion { get; set; } = "3.12.11";

    /// <summary>HTTPS ZIP archive URL for the pinned ComfyUI release (codeload — no git required).</summary>
    public string ComfyUiZipUrl { get; set; } = "https://codeload.github.com/comfyanonymous/ComfyUI/zip/refs/tags/v0.3.44";

    /// <summary>Pinned ComfyUI version tag (names the extracted archive folder).</summary>
    public string ComfyUiVersionTag { get; set; } = "v0.3.44";

    /// <summary>Optional base64 AES-256 key for source-token encryption; when empty a key file under the managed root is used.</summary>
    public string? TokenEncryptionKey { get; set; }

    /// <summary>Minimum free disk in gigabytes for the local-inference readiness verdict.</summary>
    public double MinFreeDiskGb { get; set; } = 20;

    /// <summary>Minimum physical RAM in gigabytes for the local-inference readiness verdict.</summary>
    public double MinRamGb { get; set; } = 8;

    /// <summary>Minimum logical CPU cores for the local-inference readiness verdict.</summary>
    public int MinCpuCores { get; set; } = 4;

    /// <summary>How long a detected capability report is cached in-process, in seconds.</summary>
    public int CapabilityCacheSeconds { get; set; } = 60;

    /// <summary>
    /// Builds options from configuration, reading the <c>Setup</c> section key-by-key so the
    /// engine needs no configuration-binder package; missing keys keep their defaults.
    /// </summary>
    /// <param name="aConfiguration">The application configuration.</param>
    /// <returns>The populated options instance.</returns>
    public static SetupOptions FromConfiguration(IConfiguration aConfiguration)
    {
        var vOptions = new SetupOptions();
        vOptions.ManagedEnvRoot = aConfiguration["Setup:ManagedEnvRoot"] ?? vOptions.ManagedEnvRoot;
        vOptions.NetworkVolumeRoot = aConfiguration["Setup:NetworkVolumeRoot"] ?? vOptions.NetworkVolumeRoot;
        vOptions.PythonStandaloneBaseUrl = aConfiguration["Setup:PythonStandaloneBaseUrl"] ?? vOptions.PythonStandaloneBaseUrl;
        vOptions.PythonStandaloneTag = aConfiguration["Setup:PythonStandaloneTag"] ?? vOptions.PythonStandaloneTag;
        vOptions.PythonVersion = aConfiguration["Setup:PythonVersion"] ?? vOptions.PythonVersion;
        vOptions.ComfyUiZipUrl = aConfiguration["Setup:ComfyUiZipUrl"] ?? vOptions.ComfyUiZipUrl;
        vOptions.ComfyUiVersionTag = aConfiguration["Setup:ComfyUiVersionTag"] ?? vOptions.ComfyUiVersionTag;
        vOptions.TokenEncryptionKey = aConfiguration["Setup:TokenEncryptionKey"] ?? vOptions.TokenEncryptionKey;
        if (double.TryParse(aConfiguration["Setup:MinFreeDiskGb"], out var vMinDisk)) { vOptions.MinFreeDiskGb = vMinDisk; }
        if (double.TryParse(aConfiguration["Setup:MinRamGb"], out var vMinRam)) { vOptions.MinRamGb = vMinRam; }
        if (int.TryParse(aConfiguration["Setup:MinCpuCores"], out var vMinCores)) { vOptions.MinCpuCores = vMinCores; }
        if (int.TryParse(aConfiguration["Setup:CapabilityCacheSeconds"], out var vCacheSeconds)) { vOptions.CapabilityCacheSeconds = vCacheSeconds; }
        return vOptions;
    }
}
