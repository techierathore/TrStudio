using Microsoft.Extensions.Configuration;

namespace TrStudio.Engine.Domain.RunPod;

/// <summary>
/// Configuration for the RunPod serverless provider (section <c>RunPod</c>, REQ-FN-028/BRD-12).
/// The API key and endpoint id are EMPTY by default — with no key the provider and its ops
/// endpoints degrade gracefully to a "not configured" state rather than crashing.
/// <see cref="AutoIdleStop"/> is mutable at runtime via the admin toggle, so this options object
/// is registered as a shared singleton.
/// </summary>
public class RunPodOptions
{
    /// <summary>The RunPod API key (secret). Empty disables live RunPod execution.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The RunPod serverless endpoint id jobs are submitted to.</summary>
    public string EndpointId { get; set; } = string.Empty;

    /// <summary>Base URL of the RunPod serverless REST API.</summary>
    public string BaseUrl { get; set; } = "https://api.runpod.ai/v2";

    /// <summary>Whether the endpoint auto-scales to zero (idle-stop) after an idle job completes.</summary>
    public bool AutoIdleStop { get; set; } = true;

    /// <summary>Idle timeout in seconds before the endpoint scales to zero.</summary>
    public int IdleTimeoutSeconds { get; set; } = 5;

    /// <summary>Whether a usable RunPod API key and endpoint id are configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(EndpointId);

    /// <summary>The actionable message surfaced whenever RunPod is used while not configured.</summary>
    public const string NotConfiguredMessage =
        "RunPod not configured — add RunPod:ApiKey and RunPod:EndpointId to appsettings to enable serverless GPU (High tier).";

    /// <summary>
    /// Builds options from configuration, reading the <c>RunPod</c> section key-by-key so the
    /// engine needs no configuration-binder package; missing keys keep their defaults.
    /// </summary>
    /// <param name="aConfiguration">The application configuration.</param>
    /// <returns>The populated options instance.</returns>
    public static RunPodOptions FromConfiguration(IConfiguration aConfiguration)
    {
        var vOptions = new RunPodOptions();
        vOptions.ApiKey = aConfiguration["RunPod:ApiKey"] ?? vOptions.ApiKey;
        vOptions.EndpointId = aConfiguration["RunPod:EndpointId"] ?? vOptions.EndpointId;
        vOptions.BaseUrl = aConfiguration["RunPod:BaseUrl"] ?? vOptions.BaseUrl;
        if (bool.TryParse(aConfiguration["RunPod:AutoIdleStop"], out var vAutoIdle)) { vOptions.AutoIdleStop = vAutoIdle; }
        if (int.TryParse(aConfiguration["RunPod:IdleTimeoutSeconds"], out var vIdle)) { vOptions.IdleTimeoutSeconds = vIdle; }
        return vOptions;
    }
}
