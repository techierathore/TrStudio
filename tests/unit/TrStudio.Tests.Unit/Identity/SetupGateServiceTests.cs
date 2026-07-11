using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TrStudio.Engine.Domain.SetupGate;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Identity;

/// <summary>
/// Tests for <see cref="SetupGateService"/> (REQ-FN-017, BRD-85): setup complete ⇔ at least one
/// enabled model source AND at least one ready registry model; role-appropriate redirects.
/// </summary>
public class SetupGateServiceTests
{
    private readonly IModelSourceRepository objSourceRepository = Substitute.For<IModelSourceRepository>();
    private readonly IRegistryRepository objRegistryRepository = Substitute.For<IRegistryRepository>();

    /// <summary>
    /// All four combinations of (enabled source present) × (ready model present): the gate is
    /// complete only when both are true, and the flags report each half individually.
    /// </summary>
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public async Task GateCompletesOnlyWithSourceAndModel(bool aHasEnabledSource, bool aHasReadyModel, bool aExpectedComplete)
    {
        ArrangeRepositories(aHasEnabledSource, aHasReadyModel);
        var vService = CreateService();

        var vResponse = await vService.EvaluateAsync("1", UserRole.Owner);

        Assert.Equal(aExpectedComplete, vResponse.SetupComplete);
        Assert.Equal(aHasEnabledSource, vResponse.HasEnabledSource);
        Assert.Equal(aHasReadyModel, vResponse.HasReadyModel);
    }

    /// <summary>
    /// A disabled source and a not-ready model do not satisfy the gate even though rows exist.
    /// </summary>
    [Fact]
    public async Task DisabledRowsDoNotSatisfyGate()
    {
        objSourceRepository.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ModelSource> { new() { IsEnabled = false } });
        objRegistryRepository.ListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<RegistryModel> { new() { IsReady = false } });
        var vService = CreateService();

        var vResponse = await vService.EvaluateAsync("1", UserRole.Creator);

        Assert.False(vResponse.SetupComplete);
    }

    /// <summary>
    /// When the gate is complete, each role is redirected to its default landing page
    /// (Owner/Creator → /avatar, Operator → /admin/ops).
    /// </summary>
    [Theory]
    [InlineData(UserRole.Owner, "/avatar")]
    [InlineData(UserRole.Creator, "/avatar")]
    [InlineData(UserRole.Operator, "/admin/ops")]
    public async Task CompleteGateRedirectsToRoleLanding(UserRole aRole, string aExpectedPath)
    {
        ArrangeRepositories(true, true);
        var vService = CreateService();

        var vResponse = await vService.EvaluateAsync("1", aRole);

        Assert.Equal(aExpectedPath, vResponse.RedirectPath);
    }

    /// <summary>
    /// When the gate is incomplete, admins route into setup (Owner → /admin/setup, Operator →
    /// /admin/setup/hardware) and creators to /setup-required.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Owner, "/admin/setup")]
    [InlineData(UserRole.Operator, "/admin/setup/hardware")]
    [InlineData(UserRole.Creator, "/setup-required")]
    public async Task IncompleteGateRoutesIntoSetup(UserRole aRole, string aExpectedPath)
    {
        ArrangeRepositories(false, false);
        var vService = CreateService();

        var vResponse = await vService.EvaluateAsync("1", aRole);

        Assert.Equal(aExpectedPath, vResponse.RedirectPath);
    }

    /// <summary>
    /// Repository failures (e.g. tables not yet migrated on a fresh install) degrade to a
    /// not-complete verdict instead of throwing, so login never breaks.
    /// </summary>
    [Fact]
    public async Task RepositoryFailureDegradesToIncomplete()
    {
        objSourceRepository.ListAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("relation does not exist"));
        objRegistryRepository.ListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("relation does not exist"));
        var vService = CreateService();

        var vResponse = await vService.EvaluateAsync("1", UserRole.Creator);

        Assert.False(vResponse.SetupComplete);
        Assert.Equal("/setup-required", vResponse.RedirectPath);
    }

    private void ArrangeRepositories(bool aHasEnabledSource, bool aHasReadyModel)
    {
        var vSources = aHasEnabledSource
            ? new List<ModelSource> { new() { IsEnabled = true } }
            : new List<ModelSource>();
        var vModels = aHasReadyModel
            ? new List<RegistryModel> { new() { IsReady = true } }
            : new List<RegistryModel>();
        objSourceRepository.ListAsync(Arg.Any<CancellationToken>()).Returns(vSources);
        objRegistryRepository.ListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(vModels);
    }

    private SetupGateService CreateService()
    {
        return new SetupGateService(objSourceRepository, objRegistryRepository, NullLogger<SetupGateService>.Instance);
    }
}
