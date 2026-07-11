using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Rag;

/// <summary>
/// Default <see cref="ILlmTokenTracker"/> (RAG-003/BRD-56). Accrues text-LLM token counts and cost
/// per user in-process (no Redis — in-memory rollup) and FEEDS the spend rollup: any positive-cost
/// usage is recorded to the <see cref="SpendChannel.LlmText"/> channel via the shared
/// <see cref="ISpendLedger"/>, so text-LLM spend appears in the monthly-cap report alongside the
/// RunPod and closed-API channels.
/// </summary>
public class LlmTokenTracker : ILlmTokenTracker
{
    private readonly ISpendLedger objSpendLedger;
    private readonly ILogger<LlmTokenTracker> objLogger;
    private readonly ConcurrentDictionary<string, UserUsage> objUsageByUser = new(StringComparer.Ordinal);

    /// <summary>
    /// Initialises the tracker.
    /// </summary>
    /// <param name="aSpendLedger">The shared spend ledger the LlmText channel feeds.</param>
    /// <param name="aLogger">Structured logger.</param>
    public LlmTokenTracker(ISpendLedger aSpendLedger, ILogger<LlmTokenTracker> aLogger)
    {
        objSpendLedger = aSpendLedger;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task RecordUsageAsync(string aUserId, string aModelId, int aInputTokens, int aOutputTokens, decimal aCostUsd, Guid? aJobId, CancellationToken aCancellationToken = default)
    {
        var vUserId = string.IsNullOrWhiteSpace(aUserId) ? "unknown" : aUserId;
        var vUsage = objUsageByUser.GetOrAdd(vUserId, _ => new UserUsage());
        lock (vUsage.Gate)
        {
            vUsage.InputTokens += Math.Max(0, aInputTokens);
            vUsage.OutputTokens += Math.Max(0, aOutputTokens);
            vUsage.CostUsd += aCostUsd > 0m ? aCostUsd : 0m;
            vUsage.OperationCount += 1;
        }

        // Feed the spend rollup — the ledger ignores zero/negative amounts, so free usage accrues
        // tokens without touching the cap.
        if (aCostUsd > 0m)
        {
            await objSpendLedger
                .RecordAsync(vUserId, aJobId ?? Guid.NewGuid(), aModelId, SpendChannel.LlmText, aCostUsd, aCancellationToken)
                .ConfigureAwait(false);
        }

        objLogger.LogInformation(
            "Accrued LLM usage for {UserId} on {Model}: in={In} out={Out} cost={Cost:0.####} USD (channel LlmText)",
            vUserId, aModelId, aInputTokens, aOutputTokens, aCostUsd);
    }

    /// <inheritdoc />
    public LlmUsageResponse GetUsage(string aUserId)
    {
        var vUserId = string.IsNullOrWhiteSpace(aUserId) ? "unknown" : aUserId;
        if (!objUsageByUser.TryGetValue(vUserId, out var vUsage))
        {
            return new LlmUsageResponse();
        }

        lock (vUsage.Gate)
        {
            return new LlmUsageResponse
            {
                InputTokens = vUsage.InputTokens,
                OutputTokens = vUsage.OutputTokens,
                EstimatedCostUsd = vUsage.CostUsd,
                OperationCount = vUsage.OperationCount
            };
        }
    }

    /// <summary>Mutable per-user in-memory accumulator.</summary>
    private sealed class UserUsage
    {
        public object Gate { get; } = new();
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public decimal CostUsd { get; set; }
        public int OperationCount { get; set; }
    }
}
