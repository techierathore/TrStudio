using Microsoft.Extensions.Configuration;

namespace TrStudio.Engine.Domain.Cost;

/// <summary>
/// Configuration for the cost guardrails (section <c>Cost</c>, REQ-FN-029/BRD-13, NFR-007): the
/// monthly spend cap, the warn threshold, the per-unit rates the estimator applies to each
/// registry cost kind, and whether an over-cap submit is hard-blocked or merely warned. Every
/// value has a safe default so the section is optional.
/// </summary>
public class CostOptions
{
    /// <summary>The monthly spend cap across all billed channels (default $100).</summary>
    public decimal MonthlyCapUsd { get; set; } = 100m;

    /// <summary>Fraction of the cap at which a submit is warned (default 0.8 = 80%).</summary>
    public double WarnThresholdRatio { get; set; } = 0.8d;

    /// <summary>Whether an over-cap submit is hard-blocked (true) or allowed with a warning (false).</summary>
    public bool BlockOverCap { get; set; } = true;

    /// <summary>Price per serverless GPU second used for "serverless-seconds" cost kinds (default $0.0004/s).</summary>
    public decimal RunPodPerSecondUsd { get; set; } = 0.0004m;

    /// <summary>Price per closed-API call used for "api-call"/"per-image" cost kinds (default $0.02).</summary>
    public decimal PerApiCallUsd { get; set; } = 0.02m;

    /// <summary>The account currency code.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Builds options from configuration, reading the <c>Cost</c> section key-by-key so the
    /// engine needs no configuration-binder package; missing keys keep their defaults.
    /// </summary>
    /// <param name="aConfiguration">The application configuration.</param>
    /// <returns>The populated options instance.</returns>
    public static CostOptions FromConfiguration(IConfiguration aConfiguration)
    {
        var vOptions = new CostOptions();
        if (decimal.TryParse(aConfiguration["Cost:MonthlyCapUsd"], out var vCap)) { vOptions.MonthlyCapUsd = vCap; }
        if (double.TryParse(aConfiguration["Cost:WarnThresholdRatio"], out var vWarn)) { vOptions.WarnThresholdRatio = vWarn; }
        if (bool.TryParse(aConfiguration["Cost:BlockOverCap"], out var vBlock)) { vOptions.BlockOverCap = vBlock; }
        if (decimal.TryParse(aConfiguration["Cost:RunPodPerSecondUsd"], out var vPerSec)) { vOptions.RunPodPerSecondUsd = vPerSec; }
        if (decimal.TryParse(aConfiguration["Cost:PerApiCallUsd"], out var vPerCall)) { vOptions.PerApiCallUsd = vPerCall; }
        vOptions.Currency = aConfiguration["Cost:Currency"] ?? vOptions.Currency;
        return vOptions;
    }
}
