using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Rag;
using TrStudio.Models.Enums;
using TrStudio.Models.Services;

namespace TrStudio.Tests.Unit.Rag;

/// <summary>
/// Unit tests for <see cref="LlmTokenTracker"/> (RAG-003/BRD-56): per-user token accrual, feeding the
/// LlmText spend channel for billed usage, and never recording spend for free (zero-cost) usage.
/// </summary>
public class LlmTokenTrackerTests
{
    private readonly ISpendLedger objSpendLedger = Substitute.For<ISpendLedger>();

    private LlmTokenTracker CreateTracker()
        => new(objSpendLedger, NullLogger<LlmTokenTracker>.Instance);

    /// <summary>Billed usage accrues tokens and records spend on the LlmText channel.</summary>
    [Fact]
    public async Task BilledUsageFeedsLlmTextChannel()
    {
        var vTracker = CreateTracker();

        await vTracker.RecordUsageAsync("user-1", "gpt-4o", 100, 50, 0.03m, null);

        await objSpendLedger.Received(1).RecordAsync(
            "user-1", Arg.Any<Guid>(), "gpt-4o", SpendChannel.LlmText, 0.03m, Arg.Any<CancellationToken>());

        var vUsage = vTracker.GetUsage("user-1");
        Assert.Equal(150, vUsage.TotalTokens);
        Assert.Equal(0.03m, vUsage.EstimatedCostUsd);
        Assert.Equal(1, vUsage.OperationCount);
    }

    /// <summary>Free (zero-cost) usage accrues tokens but never records spend.</summary>
    [Fact]
    public async Task FreeUsageAccruesTokensButNoSpend()
    {
        var vTracker = CreateTracker();

        await vTracker.RecordUsageAsync("user-1", "synthetic-dev", 40, 20, 0m, null);

        await objSpendLedger.DidNotReceive().RecordAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<SpendChannel>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());

        var vUsage = vTracker.GetUsage("user-1");
        Assert.Equal(60, vUsage.TotalTokens);
        Assert.Equal(0m, vUsage.EstimatedCostUsd);
    }

    /// <summary>Usage accumulates across multiple operations for the same user.</summary>
    [Fact]
    public async Task UsageAccumulatesAcrossOperations()
    {
        var vTracker = CreateTracker();

        await vTracker.RecordUsageAsync("user-1", "m", 10, 10, 0.01m, null);
        await vTracker.RecordUsageAsync("user-1", "m", 20, 20, 0.02m, null);

        var vUsage = vTracker.GetUsage("user-1");
        Assert.Equal(60, vUsage.TotalTokens);
        Assert.Equal(0.03m, vUsage.EstimatedCostUsd);
        Assert.Equal(2, vUsage.OperationCount);
    }

    /// <summary>A user with no recorded usage reads back a zeroed rollup.</summary>
    [Fact]
    public void UnknownUserReadsZeroUsage()
    {
        var vUsage = CreateTracker().GetUsage("nobody");
        Assert.Equal(0, vUsage.TotalTokens);
        Assert.Equal(0, vUsage.OperationCount);
    }
}
