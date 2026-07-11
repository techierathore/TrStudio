namespace TrStudio.Models.Entities;

/// <summary>
/// Cost declaration for a registry model (Architecture §6 <c>cost</c> block), used by the
/// cost estimator and the monthly spend guardrails.
/// </summary>
public class ModelCost
{
    /// <summary>The cost accounting kind (e.g. "serverless-seconds", "api-call", "local-free").</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Estimated execution seconds per job for time-billed providers, if known.</summary>
    public int? EstimateSecPerJob { get; set; }
}
