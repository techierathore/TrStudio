using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Voice;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Tests.Unit.Voice;

/// <summary>
/// Unit tests for <see cref="VoiceCloneService"/> (FN-032 / SAFE-03): a real-person clone is BLOCKED
/// until granted consent evidence exists, a real-person clone with consent proceeds to registration,
/// and a fully-synthetic clone proceeds ungated.
/// </summary>
public class VoiceCloneConsentGateTests
{
    private readonly IVoiceConsentService objConsentService = Substitute.For<IVoiceConsentService>();
    private readonly IVoiceCloneRepository objRepository = Substitute.For<IVoiceCloneRepository>();

    private VoiceCloneService CreateService()
        => new(objConsentService, objRepository, NullLogger<VoiceCloneService>.Instance);

    /// <summary>A real-person clone with no recorded consent is blocked and never registered.</summary>
    [Fact]
    public async Task RealPersonWithoutConsentIsBlocked()
    {
        objConsentService.HasConsentAsync("user-1", "actor-jai", Arg.Any<CancellationToken>()).Returns(false);

        var vResult = await CreateService().RequestCloneAsync(
            new VoiceCloneRequest { VoiceName = "Jai clone", IsRealPerson = true, SubjectReference = "actor-jai" }, "user-1");

        Assert.True(vResult.Blocked);
        Assert.False(vResult.ConsentVerified);
        Assert.Equal("BlockedNoConsent", vResult.Status);
        Assert.Null(vResult.CloneId);
        await objRepository.DidNotReceive().InsertAsync(Arg.Any<VoiceClone>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A real-person clone missing a subject reference is blocked before any consent lookup.</summary>
    [Fact]
    public async Task RealPersonWithoutSubjectReferenceIsBlocked()
    {
        var vResult = await CreateService().RequestCloneAsync(
            new VoiceCloneRequest { VoiceName = "No subject", IsRealPerson = true, SubjectReference = null }, "user-1");

        Assert.True(vResult.Blocked);
        Assert.Equal("BlockedNoConsent", vResult.Status);
        await objRepository.DidNotReceive().InsertAsync(Arg.Any<VoiceClone>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A real-person clone with granted consent is registered.</summary>
    [Fact]
    public async Task RealPersonWithConsentIsRegistered()
    {
        objConsentService.HasConsentAsync("user-1", "actor-jai", Arg.Any<CancellationToken>()).Returns(true);

        var vResult = await CreateService().RequestCloneAsync(
            new VoiceCloneRequest { VoiceName = "Jai clone", IsRealPerson = true, SubjectReference = "actor-jai" }, "user-1");

        Assert.False(vResult.Blocked);
        Assert.True(vResult.ConsentVerified);
        Assert.NotNull(vResult.CloneId);
        await objRepository.Received(1).InsertAsync(
            Arg.Is<VoiceClone>(aClone => aClone.IsRealPerson && aClone.SubjectReference == "actor-jai"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>A synthetic clone proceeds without any consent check.</summary>
    [Fact]
    public async Task SyntheticCloneProceedsUngated()
    {
        var vResult = await CreateService().RequestCloneAsync(
            new VoiceCloneRequest { VoiceName = "Made-up voice", IsRealPerson = false }, "user-1");

        Assert.False(vResult.Blocked);
        Assert.True(vResult.IsSynthetic);
        Assert.NotNull(vResult.CloneId);
        await objConsentService.DidNotReceive().HasConsentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await objRepository.Received(1).InsertAsync(Arg.Any<VoiceClone>(), Arg.Any<CancellationToken>());
    }
}
