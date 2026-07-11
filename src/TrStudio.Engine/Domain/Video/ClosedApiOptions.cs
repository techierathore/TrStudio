using Microsoft.Extensions.Configuration;

namespace TrStudio.Engine.Domain.Video;

/// <summary>
/// Configuration for the closed-API aggregator provider (section <c>ClosedApi</c>, FN-051/BRD-42):
/// the adapter fronting a hosted video-model aggregator (fal.ai / Replicate / WaveSpeed shape) that
/// backs the Premium video tier. The API key is EMPTY by default — with no key the provider degrades
/// gracefully to a "not configured" state carrying an actionable remediation instead of crashing;
/// live aggregator submission is deferred until a real key is supplied.
/// </summary>
public class ClosedApiOptions
{
    /// <summary>The aggregator API key (secret). Empty disables live closed-API execution.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The aggregator vendor shape: <c>fal</c>, <c>replicate</c> or <c>wavespeed</c>.</summary>
    public string Vendor { get; set; } = "fal";

    /// <summary>Base URL of the aggregator REST API.</summary>
    public string BaseUrl { get; set; } = "https://api.fal.ai";

    /// <summary>Whether a usable aggregator API key is configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>The actionable message surfaced whenever the aggregator is used while not configured.</summary>
    public const string NotConfiguredMessage =
        "Closed-API video aggregator not configured — add ClosedApi:ApiKey (fal.ai / Replicate / WaveSpeed) " +
        "to appsettings to enable the Premium video tier, or run this job on the Fast (local) or High (RunPod) tier.";

    /// <summary>
    /// Builds options from configuration, reading the <c>ClosedApi</c> section key-by-key so the
    /// engine needs no configuration-binder package; missing keys keep their defaults.
    /// </summary>
    /// <param name="aConfiguration">The application configuration.</param>
    /// <returns>The populated options instance.</returns>
    public static ClosedApiOptions FromConfiguration(IConfiguration aConfiguration)
    {
        var vOptions = new ClosedApiOptions();
        vOptions.ApiKey = aConfiguration["ClosedApi:ApiKey"] ?? vOptions.ApiKey;
        vOptions.Vendor = aConfiguration["ClosedApi:Vendor"] ?? vOptions.Vendor;
        vOptions.BaseUrl = aConfiguration["ClosedApi:BaseUrl"] ?? vOptions.BaseUrl;
        return vOptions;
    }
}
