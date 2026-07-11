using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TrStudio.Engine.Domain.Identity;
using TrStudio.Engine.Infrastructure.Identity;
using TrStudio.Models.Enums;
using TrStudio.Models.Services;

namespace TrStudio.Tests.Unit.Identity;

/// <summary>
/// Tests for <see cref="TierGateService"/> (REQ-FN-014, BRD-59): personal mode allows all
/// tiers; in multi-user mode a gated tier is refused server-side with an upgrade prompt.
/// </summary>
public class TierGateServiceTests
{
    private readonly IAppManagerClient objClient = Substitute.For<IAppManagerClient>();

    /// <summary>
    /// In personal mode every tier — including Premium — is allowed without any FeatureSvc call.
    /// </summary>
    [Theory]
    [InlineData(GenerationTier.Fast)]
    [InlineData(GenerationTier.High)]
    [InlineData(GenerationTier.Premium)]
    public async Task PersonalModeAllowsAllTiers(GenerationTier aTier)
    {
        var vGate = CreateGate(aIsPersonalMode: true);

        var vResult = await vGate.CheckTierAsync("1", aTier);

        Assert.True(vResult.IsAllowed);
        await objClient.DidNotReceiveWithAnyArgs().IsFeatureEnabledForTokenAsync(default!, default!);
        await objClient.DidNotReceiveWithAnyArgs().IsFeatureEnabledAsync(default!, default!);
    }

    /// <summary>
    /// The Fast (local) tier is never license-gated, even in multi-user mode.
    /// </summary>
    [Fact]
    public async Task FastTierAlwaysAllowed()
    {
        var vGate = CreateGate(aIsPersonalMode: false);

        var vResult = await vGate.CheckTierAsync("1", GenerationTier.Fast);

        Assert.True(vResult.IsAllowed);
    }

    /// <summary>
    /// In multi-user mode, a Premium request without the licensed feature is refused with an
    /// upgrade-prompt response naming the required feature code — the acceptance of REQ-FN-014.
    /// </summary>
    [Fact]
    public async Task GatedPremiumTierRefusedWithUpgradePrompt()
    {
        objClient.IsFeatureEnabledForTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var vGate = CreateGate(aIsPersonalMode: false);

        var vResult = await vGate.CheckTierAsync("1", GenerationTier.Premium, "token-abc");

        Assert.False(vResult.IsAllowed);
        Assert.Equal("TIER_GATED", vResult.DeniedReason);
        Assert.Equal("TIER_PREMIUM", vResult.RequiredFeatureCode);
        Assert.False(string.IsNullOrWhiteSpace(vResult.UpgradeMessage));
    }

    /// <summary>
    /// In multi-user mode, a licensed feature unlocks the gated tier.
    /// </summary>
    [Fact]
    public async Task LicensedFeatureUnlocksTier()
    {
        objClient.IsFeatureEnabledForTokenAsync("token-abc", "TIER_HIGH", Arg.Any<CancellationToken>())
            .Returns(true);
        var vGate = CreateGate(aIsPersonalMode: false);

        var vResult = await vGate.CheckTierAsync("1", GenerationTier.High, "token-abc");

        Assert.True(vResult.IsAllowed);
    }

    /// <summary>
    /// Without a request token the gate falls back to the user-id based feature check (worker
    /// path) and still refuses when the feature is not licensed.
    /// </summary>
    [Fact]
    public async Task WorkerPathChecksByUserIdAndFailsClosed()
    {
        objClient.IsFeatureEnabledAsync("42", "TIER_PREMIUM", Arg.Any<CancellationToken>())
            .Returns(false);
        var vGate = CreateGate(aIsPersonalMode: false);

        var vResult = await vGate.CheckTierAsync("42", GenerationTier.Premium);

        Assert.False(vResult.IsAllowed);
        await objClient.Received(1).IsFeatureEnabledAsync("42", "TIER_PREMIUM", Arg.Any<CancellationToken>());
    }

    private TierGateService CreateGate(bool aIsPersonalMode)
    {
        var vOptions = Options.Create(new AppManagerOptions { PersonalMode = aIsPersonalMode });
        return new TierGateService(objClient, vOptions, NullLogger<TierGateService>.Instance);
    }
}
