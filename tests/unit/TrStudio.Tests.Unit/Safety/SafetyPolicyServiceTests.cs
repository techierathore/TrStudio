using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Safety;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Safety;

/// <summary>
/// Unit tests for <see cref="SafetyPolicyService"/> (FN-062 / F-NSFW / BRD-79): the NSFW mode is
/// off by default, is only effective in personal/single-user mode, and is force-refused (never
/// persisted enabled) in multi-user/SaaS mode.
/// </summary>
public class SafetyPolicyServiceTests
{
    private readonly ISafetyPolicyRepository objRepository = Substitute.For<ISafetyPolicyRepository>();

    private SafetyPolicyService CreateService(bool aPersonalMode)
    {
        var vConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AppManager:PersonalMode"] = aPersonalMode.ToString() })
            .Build();
        return new SafetyPolicyService(objRepository, vConfig, NullLogger<SafetyPolicyService>.Instance);
    }

    /// <summary>By default (no stored row) the mode is off.</summary>
    [Fact]
    public async Task DefaultsToOff()
    {
        objRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((SafetyPolicy?)null);

        var vState = await CreateService(aPersonalMode: true).GetNsfwPolicyAsync();

        Assert.False(vState.StoredNsfwEnabled);
        Assert.False(vState.EffectiveNsfwEnabled);
    }

    /// <summary>In personal mode the owner can enable the mode and it becomes effective.</summary>
    [Fact]
    public async Task PersonalModeEnableIsEffective()
    {
        var vState = await CreateService(aPersonalMode: true).SetNsfwEnabledAsync(true);

        Assert.True(vState.StoredNsfwEnabled);
        Assert.True(vState.EffectiveNsfwEnabled);
        await objRepository.Received().UpsertAsync(
            Arg.Is<SafetyPolicy>(aPolicy => aPolicy.NsfwModeEnabled), Arg.Any<CancellationToken>());
    }

    /// <summary>In multi-user mode enabling is REFUSED and never persisted true (auto-off, F-NSFW).</summary>
    [Fact]
    public async Task MultiUserModeEnableIsRefusedAndForcedOff()
    {
        var vState = await CreateService(aPersonalMode: false).SetNsfwEnabledAsync(true);

        Assert.False(vState.StoredNsfwEnabled);
        Assert.False(vState.EffectiveNsfwEnabled);
        Assert.False(vState.PersonalMode);
        await objRepository.Received().UpsertAsync(
            Arg.Is<SafetyPolicy>(aPolicy => !aPolicy.NsfwModeEnabled), Arg.Any<CancellationToken>());
    }

    /// <summary>Even a stored "enabled" row resolves to NOT effective when the deployment is multi-user.</summary>
    [Fact]
    public async Task StoredEnabledIsNotEffectiveInMultiUser()
    {
        objRepository.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new SafetyPolicy { PolicyKey = "default", NsfwModeEnabled = true });

        var vState = await CreateService(aPersonalMode: false).GetNsfwPolicyAsync();

        Assert.True(vState.StoredNsfwEnabled);
        Assert.False(vState.EffectiveNsfwEnabled);
    }
}
