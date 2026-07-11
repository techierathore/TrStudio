using Microsoft.Extensions.Logging;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Cost;

/// <summary>
/// Default <see cref="ISpendLedger"/> (REQ-FN-029/BRD-13, NFR-007). Persists actual spend per
/// completed billed job across the RunPod and closed-API channels, reports month-to-date spend
/// against the configured monthly cap, and evaluates a pending estimate at submit so the
/// orchestrator can warn near the cap or block over it.
/// </summary>
public class SpendLedger : ISpendLedger
{
    private readonly ISpendRepository objSpendRepository;
    private readonly CostOptions objCostOptions;
    private readonly ILogger<SpendLedger> objLogger;

    /// <summary>
    /// Initialises the ledger.
    /// </summary>
    /// <param name="aSpendRepository">Spend-record persistence and aggregation.</param>
    /// <param name="aCostOptions">Cost guardrail options carrying the cap and warn threshold.</param>
    /// <param name="aLogger">Structured logger (records amounts, never secrets).</param>
    public SpendLedger(ISpendRepository aSpendRepository, CostOptions aCostOptions, ILogger<SpendLedger> aLogger)
    {
        objSpendRepository = aSpendRepository;
        objCostOptions = aCostOptions;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task RecordAsync(string aUserId, Guid aJobId, string aModelId, SpendChannel aChannel, decimal aAmountUsd, CancellationToken aCancellationToken = default)
    {
        if (aAmountUsd <= 0m || aChannel == SpendChannel.Local)
        {
            // Free/local execution never accrues spend against the cap.
            return;
        }

        await objSpendRepository.InsertAsync(new SpendRecord
        {
            SpendRecordId = Guid.NewGuid(),
            UserId = aUserId,
            JobId = aJobId,
            ModelId = aModelId,
            Channel = aChannel,
            AmountUsd = aAmountUsd,
            CreatedUtc = DateTime.UtcNow
        }, aCancellationToken).ConfigureAwait(false);

        objLogger.LogInformation(
            "Recorded {Amount} {Currency} spend on channel {Channel} for job {JobId}",
            aAmountUsd, objCostOptions.Currency, aChannel, aJobId);
    }

    /// <inheritdoc />
    public async Task<SpendSummaryResponse> GetSummaryAsync(CancellationToken aCancellationToken = default)
    {
        var vMonthStart = StartOfCurrentMonthUtc();
        var vByChannel = await objSpendRepository.GetTotalsByChannelSinceAsync(vMonthStart, aCancellationToken).ConfigureAwait(false);
        var vSpent = vByChannel.Values.Sum();
        var vCap = objCostOptions.MonthlyCapUsd;
        var vRemaining = Math.Max(0m, vCap - vSpent);

        return new SpendSummaryResponse
        {
            MonthlyCapUsd = vCap,
            SpentToDateUsd = vSpent,
            RemainingUsd = vRemaining,
            ByChannel = vByChannel.ToDictionary(aPair => aPair.Key.ToString(), aPair => aPair.Value),
            Currency = objCostOptions.Currency,
            NearCap = vCap > 0m && vSpent >= vCap * (decimal)objCostOptions.WarnThresholdRatio,
            OverCap = vCap > 0m && vSpent >= vCap
        };
    }

    /// <inheritdoc />
    public async Task<SpendGuardResult> EvaluateAsync(decimal aPendingEstimate, CancellationToken aCancellationToken = default)
    {
        var vMonthStart = StartOfCurrentMonthUtc();
        var vSpent = await objSpendRepository.GetTotalSinceAsync(vMonthStart, aCancellationToken).ConfigureAwait(false);
        var vCap = objCostOptions.MonthlyCapUsd;
        var vProjected = vSpent + aPendingEstimate;

        // Free/local jobs (zero estimate) are never blocked, even if the cap is already reached.
        var vBilled = aPendingEstimate > 0m;
        var vWouldExceed = vBilled && vCap > 0m && vProjected > vCap;
        var vWarnFloor = vCap * (decimal)objCostOptions.WarnThresholdRatio;
        var vShouldWarn = vBilled && !vWouldExceed && vCap > 0m && vProjected > vWarnFloor;

        string? vMessage = null;
        if (vWouldExceed)
        {
            vMessage =
                $"This job's estimated {Money(aPendingEstimate)} would push month-to-date spend to " +
                $"{Money(vProjected)}, over the {Money(vCap)} monthly cap. Raise the cap under " +
                "Admin > Spend, wait for next month, or run it on the free local (Fast) tier.";
        }
        else if (vShouldWarn)
        {
            vMessage =
                $"Heads up: this job brings month-to-date spend to {Money(vProjected)} of the " +
                $"{Money(vCap)} monthly cap.";
        }

        return new SpendGuardResult(vSpent, vCap, vProjected, vWouldExceed, vShouldWarn, vMessage);
    }

    private string Money(decimal aAmount) => $"{objCostOptions.Currency} {aAmount:0.00}";

    private static DateTime StartOfCurrentMonthUtc()
    {
        var vNow = DateTime.UtcNow;
        return new DateTime(vNow.Year, vNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}
