using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Safety;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Tests.Unit.Safety;

/// <summary>
/// Unit tests for <see cref="SafetyPipeline"/> proving each non-disableable floor (SAFE-01..06 /
/// FN-056..063) blocks its trigger and that every decision is audited. Uses the real fail-closed
/// default detectors with the Development test seam on, so a well-known sentinel forces the flag.
/// The floors are proven to remain enforced with NSFW mode ON.
/// </summary>
public class SafetyPipelineTests
{
    private readonly IConsentService objConsentService = Substitute.For<IConsentService>();
    private readonly IPublicFigureRightsRepository objRightsRepository = Substitute.For<IPublicFigureRightsRepository>();
    private readonly ISafetyPolicyService objPolicyService = Substitute.For<ISafetyPolicyService>();
    private readonly ISafetyAuditRepository objAuditRepository = Substitute.For<ISafetyAuditRepository>();
    private readonly SafetyOptions objOptions = new() { TestSeamEnabled = true };

    private SafetyPipeline CreatePipeline()
    {
        return new SafetyPipeline(
            new HeuristicContentClassifier(objOptions, NullLogger<HeuristicContentClassifier>.Instance),
            new UnavailableHashMatcher(),
            new HeuristicPublicFigureMatcher(objOptions),
            objConsentService,
            objRightsRepository,
            objPolicyService,
            objAuditRepository,
            NullLogger<SafetyPipeline>.Instance);
    }

    private static SafetyEvaluationContext Context(string aParamsJson, GenerationTaskType aTask = GenerationTaskType.TextToImage)
        => new()
        {
            JobId = Guid.NewGuid(),
            UserId = "user-1",
            ModelId = "stub-echo",
            Task = aTask,
            Tier = GenerationTier.Fast,
            ParamsJson = aParamsJson,
            SafetyClass = SafetyClass.SyntheticDefault
        };

    /// <summary>SAFE-01 (FN-056): a CSAM-flagged input is blocked at pre-check and the decision is audited.</summary>
    [Fact]
    public async Task Safe01CsamInputIsBlockedAndAudited()
    {
        var vDecision = await CreatePipeline().PreCheckAsync(Context("{\"prompt\":\"" + SafetyOptions.CsamSentinel + "\"}"));

        Assert.False(vDecision.Allowed);
        Assert.Equal(SafetyFloor.Csam, vDecision.Floor);
        await objAuditRepository.Received().InsertAsync(
            Arg.Is<SafetyAuditEntry>(aEntry => !aEntry.Allowed && aEntry.Floor == SafetyFloor.Csam), Arg.Any<CancellationToken>());
    }

    /// <summary>SAFE-01: a classifier that throws fails CLOSED (blocks), never allows on uncertainty.</summary>
    [Fact]
    public async Task Safe01ClassifierErrorFailsClosed()
    {
        var vThrowingClassifier = Substitute.For<IContentClassifier>();
        vThrowingClassifier.ClassifyAsync(Arg.Any<SafetyContentSample>(), Arg.Any<SafetyPhase>(), Arg.Any<CancellationToken>())
            .Returns<Task<ContentClassification>>(_ => throw new InvalidOperationException("model down"));

        var vPipeline = new SafetyPipeline(
            vThrowingClassifier, new UnavailableHashMatcher(), new HeuristicPublicFigureMatcher(objOptions),
            objConsentService, objRightsRepository, objPolicyService, objAuditRepository, NullLogger<SafetyPipeline>.Instance);

        var vDecision = await vPipeline.PreCheckAsync(Context("{\"prompt\":\"a synthetic landscape\"}"));

        Assert.False(vDecision.Allowed);
        Assert.Equal(SafetyFloor.Csam, vDecision.Floor);
    }

    /// <summary>SAFE-02 (FN-057): sexual content of a real, identifiable person is refused (NCII).</summary>
    [Fact]
    public async Task Safe02RealPersonSexualIsBlocked()
    {
        var vDecision = await CreatePipeline().PreCheckAsync(
            Context("{\"prompt\":\"portrait\",\"isRealPerson\":true,\"subjectReference\":\"actor-x\",\"nsfw\":true}"));

        Assert.False(vDecision.Allowed);
        Assert.Equal(SafetyFloor.RealPersonSexual, vDecision.Floor);
    }

    /// <summary>SAFE-03 (FN-058): a real-person face job without recorded consent is blocked (fail closed).</summary>
    [Fact]
    public async Task Safe03RealPersonWithoutConsentIsBlocked()
    {
        objConsentService.HasConsentAsync("user-1", "actor-x", ConsentModality.Face, Arg.Any<CancellationToken>()).Returns(false);

        var vDecision = await CreatePipeline().PreCheckAsync(
            Context("{\"prompt\":\"portrait\",\"isRealPerson\":true,\"subjectReference\":\"actor-x\"}"));

        Assert.False(vDecision.Allowed);
        Assert.Equal(SafetyFloor.Consent, vDecision.Floor);
    }

    /// <summary>SAFE-03: with recorded consent the same real-person face job is allowed.</summary>
    [Fact]
    public async Task Safe03RealPersonWithConsentIsAllowed()
    {
        objConsentService.HasConsentAsync("user-1", "actor-x", ConsentModality.Face, Arg.Any<CancellationToken>()).Returns(true);

        var vDecision = await CreatePipeline().PreCheckAsync(
            Context("{\"prompt\":\"portrait\",\"isRealPerson\":true,\"subjectReference\":\"actor-x\"}"));

        Assert.True(vDecision.Allowed);
    }

    /// <summary>SAFE-05 (FN-060): a public-figure reference without a rights record is blocked by default.</summary>
    [Fact]
    public async Task Safe05PublicFigureWithoutRightsIsBlocked()
    {
        objRightsRepository.HasRightsAsync("user-1", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var vDecision = await CreatePipeline().PreCheckAsync(
            Context("{\"prompt\":\"" + SafetyOptions.PublicFigureSentinel + "\"}"));

        Assert.False(vDecision.Allowed);
        Assert.Equal(SafetyFloor.PublicFigure, vDecision.Floor);
    }

    /// <summary>SAFE-05: with a recorded rights record the public-figure depiction is allowed.</summary>
    [Fact]
    public async Task Safe05PublicFigureWithRightsIsAllowed()
    {
        objRightsRepository.HasRightsAsync("user-1", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var vDecision = await CreatePipeline().PreCheckAsync(
            Context("{\"prompt\":\"" + SafetyOptions.PublicFigureSentinel + "\",\"publicFigureReference\":\"figure-x\"}"));

        Assert.True(vDecision.Allowed);
    }

    /// <summary>An ordinary fully-synthetic SFW job is allowed and audited as allowed.</summary>
    [Fact]
    public async Task OrdinarySyntheticJobIsAllowed()
    {
        var vDecision = await CreatePipeline().PreCheckAsync(Context("{\"prompt\":\"a synthetic fantasy castle\"}"));

        Assert.True(vDecision.Allowed);
        await objAuditRepository.Received().InsertAsync(
            Arg.Is<SafetyAuditEntry>(aEntry => aEntry.Allowed), Arg.Any<CancellationToken>());
    }

    /// <summary>FN-063: adult content is blocked by the general SFW filter when NSFW mode is OFF.</summary>
    [Fact]
    public async Task NsfwOffBlocksAdultContentViaGeneralFilter()
    {
        objPolicyService.GetNsfwPolicyAsync(Arg.Any<CancellationToken>())
            .Returns(new NsfwPolicyResponse { StoredNsfwEnabled = false, PersonalMode = true, EffectiveNsfwEnabled = false });

        var vDecision = await CreatePipeline().PreCheckAsync(
            Context("{\"prompt\":\"synthetic adult persona\",\"nsfw\":true,\"synthetic\":true,\"adult\":true}"));

        Assert.False(vDecision.Allowed);
        Assert.Equal(SafetyFloor.GeneralSfwFilter, vDecision.Floor);
    }

    /// <summary>FN-062/063: with NSFW mode ON, a fully-synthetic adult (18+) persona job is allowed.</summary>
    [Fact]
    public async Task NsfwOnAllowsSyntheticAdultPersona()
    {
        objPolicyService.GetNsfwPolicyAsync(Arg.Any<CancellationToken>())
            .Returns(new NsfwPolicyResponse { StoredNsfwEnabled = true, PersonalMode = true, EffectiveNsfwEnabled = true });

        var vDecision = await CreatePipeline().PreCheckAsync(
            Context("{\"prompt\":\"synthetic adult persona\",\"nsfw\":true,\"synthetic\":true,\"adult\":true}"));

        Assert.True(vDecision.Allowed);
    }

    /// <summary>
    /// FN-063: the floors STILL fire with NSFW mode ON — a minor-flagged NSFW job is blocked even
    /// though the mode is enabled (SAFE-01/NSFW scope). NSFW mode never lifts a floor.
    /// </summary>
    [Fact]
    public async Task NsfwOnStillBlocksMinorContent()
    {
        objPolicyService.GetNsfwPolicyAsync(Arg.Any<CancellationToken>())
            .Returns(new NsfwPolicyResponse { StoredNsfwEnabled = true, PersonalMode = true, EffectiveNsfwEnabled = true });

        var vDecision = await CreatePipeline().PreCheckAsync(
            Context("{\"prompt\":\"" + SafetyOptions.MinorSentinel + "\",\"nsfw\":true,\"synthetic\":true,\"adult\":true}"));

        Assert.False(vDecision.Allowed);
        Assert.Equal(SafetyFloor.Csam, vDecision.Floor);
    }

    /// <summary>
    /// FN-063: with NSFW mode ON a photoreal-of-real-person adult job is still refused by the NSFW
    /// scope guard (BRD-80) — the mode is limited to fully-synthetic personas.
    /// </summary>
    [Fact]
    public async Task NsfwOnStillBlocksPhotorealOfRealPerson()
    {
        objPolicyService.GetNsfwPolicyAsync(Arg.Any<CancellationToken>())
            .Returns(new NsfwPolicyResponse { StoredNsfwEnabled = true, PersonalMode = true, EffectiveNsfwEnabled = true });

        var vDecision = await CreatePipeline().PreCheckAsync(
            Context("{\"prompt\":\"adult persona\",\"nsfw\":true,\"adult\":true,\"photorealOfRealPerson\":true}"));

        Assert.False(vDecision.Allowed);
        // Real-person + sexual trips SAFE-02 first; either way it is blocked with the mode ON.
        Assert.True(vDecision.Floor is SafetyFloor.RealPersonSexual or SafetyFloor.NsfwScope);
    }

    /// <summary>FN-056: the post-check blocks a flagged OUTPUT that passed the input pre-check.</summary>
    [Fact]
    public async Task Safe01PostCheckBlocksFlaggedOutput()
    {
        var vContext = Context("{\"prompt\":\"" + SafetyOptions.OutputBlockSentinel + "\"}");

        // The output sentinel is ignored at pre-check (proving the job would run) ...
        var vPre = await CreatePipeline().PreCheckAsync(vContext);
        Assert.True(vPre.Allowed);

        // ... but the same generation is blocked at the output post-check.
        var vPost = await CreatePipeline().PostCheckAsync(vContext, new SafetyContentSample { Content = new byte[] { 1, 2, 3 }, MediaKind = "image" });
        Assert.False(vPost.Allowed);
        Assert.Equal(SafetyFloor.Csam, vPost.Floor);
    }
}
