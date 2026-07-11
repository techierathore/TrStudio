using System.Text.Json;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Cost;

/// <summary>
/// Default <see cref="ICostEstimator"/> (REQ-FN-029/NFR-007). Derives a per-job estimate from
/// the registry model's declarative <c>Cost</c> block (Architecture §6): serverless-second
/// backends bill by estimated seconds × the RunPod per-second rate, closed-API/per-image
/// backends bill by a per-call rate × the requested batch count, and local/free execution is
/// always zero.
/// </summary>
public class CostEstimator : ICostEstimator
{
    private readonly IRegistryRepository objRegistryRepository;
    private readonly CostOptions objCostOptions;

    /// <summary>
    /// Initialises the estimator.
    /// </summary>
    /// <param name="aRegistryRepository">Registry data access (the resolved model carries the cost block).</param>
    /// <param name="aCostOptions">Cost guardrail options carrying the per-unit rates.</param>
    public CostEstimator(IRegistryRepository aRegistryRepository, CostOptions aCostOptions)
    {
        objRegistryRepository = aRegistryRepository;
        objCostOptions = aCostOptions;
    }

    /// <inheritdoc />
    public async Task<decimal> EstimateAsync(GenerationJob aJob, CancellationToken aCancellationToken = default)
    {
        var vModel = await objRegistryRepository.GetAsync(aJob.ModelId, aCancellationToken).ConfigureAwait(false);
        if (vModel is null)
        {
            return 0m;
        }

        return EstimateFor(vModel.Cost, aJob.ParamsJson);
    }

    /// <summary>
    /// Computes the estimate for a cost block and a job's params (batch count) without a database
    /// round-trip — the pure pricing rule, exposed for unit testing.
    /// </summary>
    /// <param name="aCost">The registry model cost declaration.</param>
    /// <param name="aParamsJson">The job params JSON (read for a batch/count multiplier).</param>
    /// <returns>The estimated cost in the account currency; zero for free/local execution.</returns>
    public decimal EstimateFor(ModelCost aCost, string aParamsJson)
    {
        var vKind = (aCost.Kind ?? string.Empty).Trim().ToLowerInvariant();
        var vBatch = Math.Max(1, ReadBatchCount(aParamsJson));

        return vKind switch
        {
            "serverless-seconds" => objCostOptions.RunPodPerSecondUsd * Math.Max(0, aCost.EstimateSecPerJob ?? 0) * vBatch,
            "api-call" => objCostOptions.PerApiCallUsd * vBatch,
            "per-image" => objCostOptions.PerApiCallUsd * vBatch,
            "local-free" => 0m,
            "free" => 0m,
            "" => 0m,
            _ => 0m
        };
    }

    private static int ReadBatchCount(string aParamsJson)
    {
        if (string.IsNullOrWhiteSpace(aParamsJson))
        {
            return 1;
        }

        try
        {
            using var vDocument = JsonDocument.Parse(aParamsJson);
            if (vDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                return 1;
            }

            foreach (var vKey in new[] { "batch", "count", "numImages", "batchSize" })
            {
                if (vDocument.RootElement.TryGetProperty(vKey, out var vValue))
                {
                    if (vValue.ValueKind == JsonValueKind.Number && vValue.TryGetInt32(out var vNumber))
                    {
                        return vNumber;
                    }

                    if (vValue.ValueKind == JsonValueKind.String && int.TryParse(vValue.GetString(), out var vParsed))
                    {
                        return vParsed;
                    }
                }
            }
        }
        catch (JsonException)
        {
            return 1;
        }

        return 1;
    }
}
