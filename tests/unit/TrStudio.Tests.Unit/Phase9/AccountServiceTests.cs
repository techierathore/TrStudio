using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Account;
using TrStudio.Engine.Domain.Cost;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Tests.Unit.Phase9;

/// <summary>
/// Unit tests for <see cref="AccountService"/> (REQ-FN-068/BRD-52): per-user spend aggregates by
/// channel, the licence reads from AppManager when a token is present, and — critically — the
/// licence block degrades gracefully (without breaking spend) when AppManager is unreachable or no
/// token is available.
/// </summary>
public class AccountServiceTests
{
    private readonly ISpendRepository objSpendRepository = Substitute.For<ISpendRepository>();
    private readonly IAppManagerClient objAppManager = Substitute.For<IAppManagerClient>();
    private readonly CostOptions objCostOptions = new() { Currency = "USD" };

    private AccountService CreateService() => new(objSpendRepository, objAppManager, objCostOptions, NullLogger<AccountService>.Instance);

    private void SeedSpend()
        => objSpendRepository.GetTotalsByChannelForUserSinceAsync("user-1", Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<SpendChannel, decimal> { [SpendChannel.RunPod] = 5m, [SpendChannel.ClosedApi] = 3m });

    /// <summary>Spend aggregates per user by channel and the licence reads through when a token is present.</summary>
    [Fact]
    public async Task AggregatesSpendAndReadsLicense()
    {
        SeedSpend();
        objAppManager.GetLicenseStatusAsync("token-abc", Arg.Any<CancellationToken>())
            .Returns(new LicenseStatusResponse { HasLicense = true, IsValid = true, Status = "Active" });

        var vSummary = await CreateService().GetSummaryAsync("user-1", "token-abc", CancellationToken.None);

        Assert.Equal(8m, vSummary.Spend.MonthToDateUsd);
        Assert.Equal(5m, vSummary.Spend.ByChannel["RunPod"]);
        Assert.True(vSummary.License.Available);
        Assert.Equal("Active", vSummary.License.Status!.Status);
    }

    /// <summary>An unreachable AppManager degrades the licence block but leaves spend intact.</summary>
    [Fact]
    public async Task DegradesLicenseWhenAppManagerUnreachable()
    {
        SeedSpend();
        objAppManager.GetLicenseStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<LicenseStatusResponse>(_ => throw new InvalidOperationException("AppManager down"));

        var vSummary = await CreateService().GetSummaryAsync("user-1", "token-abc", CancellationToken.None);

        Assert.Equal(8m, vSummary.Spend.MonthToDateUsd);
        Assert.False(vSummary.License.Available);
        Assert.Null(vSummary.License.Status);
    }

    /// <summary>A missing bearer token degrades the licence to unavailable without touching AppManager.</summary>
    [Fact]
    public async Task DegradesLicenseWhenNoToken()
    {
        SeedSpend();

        var vSummary = await CreateService().GetSummaryAsync("user-1", null, CancellationToken.None);

        Assert.Equal(8m, vSummary.Spend.MonthToDateUsd);
        Assert.False(vSummary.License.Available);
        await objAppManager.DidNotReceive().GetLicenseStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
