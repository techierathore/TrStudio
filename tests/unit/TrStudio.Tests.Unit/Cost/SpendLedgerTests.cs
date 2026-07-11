using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Cost;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Cost;

/// <summary>
/// Unit tests for <see cref="SpendLedger"/> (REQ-FN-029/BRD-13, NFR-007): recording spend only for
/// billed channels, the month-to-date summary, and the submit-time cap evaluation (block over cap,
/// warn near cap, and never blocking a free/local job).
/// </summary>
public class SpendLedgerTests
{
    private readonly ISpendRepository objRepository = Substitute.For<ISpendRepository>();
    private readonly CostOptions objOptions = new() { MonthlyCapUsd = 100m, WarnThresholdRatio = 0.8d };

    private SpendLedger CreateLedger() => new(objRepository, objOptions, NullLogger<SpendLedger>.Instance);

    /// <summary>A billed RunPod cost is persisted as a spend record.</summary>
    [Fact]
    public async Task RecordPersistsBilledSpend()
    {
        await CreateLedger().RecordAsync("user-1", Guid.NewGuid(), "runpod-model", SpendChannel.RunPod, 4.50m);

        await objRepository.Received(1).InsertAsync(
            Arg.Is<TrStudio.Models.Entities.SpendRecord>(aRecord =>
                aRecord.Channel == SpendChannel.RunPod && aRecord.AmountUsd == 4.50m),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Local/free execution and zero amounts are never recorded.</summary>
    [Theory]
    [InlineData(SpendChannel.Local, 5.0)]
    [InlineData(SpendChannel.RunPod, 0.0)]
    public async Task RecordSkipsFreeSpend(SpendChannel aChannel, double aAmount)
    {
        await CreateLedger().RecordAsync("user-1", Guid.NewGuid(), "m", aChannel, (decimal)aAmount);

        await objRepository.DidNotReceive().InsertAsync(Arg.Any<TrStudio.Models.Entities.SpendRecord>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A billed estimate that pushes projected spend over the cap is flagged to block.</summary>
    [Fact]
    public async Task EvaluateBlocksOverCap()
    {
        objRepository.GetTotalSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(90m);

        var vGuard = await CreateLedger().EvaluateAsync(20m);

        Assert.True(vGuard.WouldExceed);
        Assert.False(vGuard.ShouldWarn);
        Assert.NotNull(vGuard.Message);
    }

    /// <summary>A billed estimate crossing the warn threshold but under the cap warns without blocking.</summary>
    [Fact]
    public async Task EvaluateWarnsNearCap()
    {
        objRepository.GetTotalSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(70m);

        var vGuard = await CreateLedger().EvaluateAsync(15m); // projected 85 > 80 warn floor, < 100 cap

        Assert.False(vGuard.WouldExceed);
        Assert.True(vGuard.ShouldWarn);
        Assert.NotNull(vGuard.Message);
    }

    /// <summary>A free (zero-estimate) job is never blocked, even when the cap is already reached.</summary>
    [Fact]
    public async Task EvaluateNeverBlocksFreeJob()
    {
        objRepository.GetTotalSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(150m);

        var vGuard = await CreateLedger().EvaluateAsync(0m);

        Assert.False(vGuard.WouldExceed);
        Assert.False(vGuard.ShouldWarn);
        Assert.Null(vGuard.Message);
    }

    /// <summary>The summary sums per-channel spend and reports remaining headroom and the over-cap flag.</summary>
    [Fact]
    public async Task SummaryAggregatesByChannel()
    {
        objRepository.GetTotalsByChannelSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<SpendChannel, decimal>
            {
                [SpendChannel.RunPod] = 60m,
                [SpendChannel.ClosedApi] = 25m
            });

        var vSummary = await CreateLedger().GetSummaryAsync();

        Assert.Equal(85m, vSummary.SpentToDateUsd);
        Assert.Equal(15m, vSummary.RemainingUsd);
        Assert.True(vSummary.NearCap);
        Assert.False(vSummary.OverCap);
        Assert.Equal(60m, vSummary.ByChannel["RunPod"]);
    }
}
