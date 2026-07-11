using Microsoft.Extensions.Configuration;

namespace TrStudio.Engine.Domain.Comfy;

/// <summary>
/// Configuration for the ComfyUI pipeline (section <c>ComfyUI</c>, FN-043..050/BRD-34..41). The
/// base URL is EMPTY by default — with no URL the <see cref="Providers.ComfyUiProvider"/> and the
/// sandbox smoke degrade gracefully to an actionable "not configured" state rather than crashing.
/// <see cref="SandboxSyntheticFallback"/> mirrors the Voice/Rag synthetic-fallback idiom: a
/// dev/personal-only switch that lets the sandbox smoke synthesise a pass when no live ComfyUI is
/// reachable, so the go-live gate can be exercised end-to-end offline. It is OFF by default and
/// must never be enabled in a real deployment.
/// </summary>
public class ComfyUiOptions
{
    /// <summary>Base URL of the ComfyUI HTTP API (e.g. <c>http://127.0.0.1:8188</c>). Empty disables live execution.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Dev/personal-only: when true, a sandbox smoke with no reachable ComfyUI synthesises a
    /// passing verdict so the go-live gate can be exercised offline. OFF in any real deployment.
    /// </summary>
    public bool SandboxSyntheticFallback { get; set; }

    /// <summary>Whether a usable ComfyUI base URL is configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);

    /// <summary>The actionable message surfaced whenever ComfyUI is used while not configured.</summary>
    public const string NotConfiguredMessage =
        "ComfyUI not configured — set ComfyUI:BaseUrl (e.g. http://127.0.0.1:8188) to run workflows on local MPS, " +
        "or route the model to the RunPod (High) tier.";

    /// <summary>
    /// Builds options from configuration, reading the <c>ComfyUI</c> section key-by-key so the
    /// engine needs no configuration-binder package; missing keys keep their defaults.
    /// </summary>
    /// <param name="aConfiguration">The application configuration.</param>
    /// <returns>The populated options instance.</returns>
    public static ComfyUiOptions FromConfiguration(IConfiguration aConfiguration)
    {
        var vOptions = new ComfyUiOptions();
        vOptions.BaseUrl = aConfiguration["ComfyUI:BaseUrl"] ?? vOptions.BaseUrl;
        if (bool.TryParse(aConfiguration["ComfyUI:SandboxSyntheticFallback"], out var vFallback))
        {
            vOptions.SandboxSyntheticFallback = vFallback;
        }

        return vOptions;
    }
}
