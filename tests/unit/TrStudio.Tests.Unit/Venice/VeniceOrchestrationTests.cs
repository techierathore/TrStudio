using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Comfy;
using TrStudio.Engine.Domain.Cost;
using TrStudio.Engine.Domain.Jobs;
using TrStudio.Engine.Domain.Progress;
using TrStudio.Engine.Domain.Providers;
using TrStudio.Engine.Domain.Registry;
using TrStudio.Engine.Domain.Safety;
using TrStudio.Engine.Domain.Venice;
using TrStudio.Engine.Providers;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Tests.Unit.Venice;

/// <summary>
/// The real <see cref="JobOrchestrator"/> and <see cref="ProviderResolver"/> over a real Venice
/// provider and a fake Venice that counts every HTTP call: the safety floors run before any Venice
/// call (REQ-FN-083), the Venice price is checked against the monthly cap before anything is sent
/// (REQ-FN-085), and ComfyUI rows still resolve to ComfyUiProvider (REQ-NFR-011).
/// </summary>
public sealed class VeniceOrchestrationTests : IDisposable
{
    private readonly VeniceTestKit objKit = new();
    private readonly IJobRepository objJobRepository = Substitute.For<IJobRepository>();
    private readonly IGenerationLogRepository objLogRepository = Substitute.For<IGenerationLogRepository>();
    private readonly IModelCatalogService objCatalog = Substitute.For<IModelCatalogService>();
    private readonly IBackgroundJobClient objBackgroundJobClient = Substitute.For<IBackgroundJobClient>();
    private readonly ICostEstimator objCostEstimator = Substitute.For<ICostEstimator>();
    private readonly ISpendLedger objSpendLedger = Substitute.For<ISpendLedger>();
    private readonly ISafetyPipeline objSafetyPipeline = Substitute.For<ISafetyPipeline>();

    private JobOrchestrator CreateOrchestrator(params IGenerationProvider[] aExtraProviders)
    {
        var vProviders = new List<IGenerationProvider> { objKit.CreateProvider() };
        vProviders.AddRange(aExtraProviders);
        return new JobOrchestrator(
            objJobRepository,
            Substitute.For<IAssetRepository>(),
            objLogRepository,
            objCatalog,
            new ProviderResolver(vProviders),
            objBackgroundJobClient,
            Substitute.For<IProgressNotifier>(),
            objCostEstimator,
            objSpendLedger,
            objSafetyPipeline,
            new CostOptions(),
            NullLogger<JobOrchestrator>.Instance);
    }

    private RegistryModel AddVisible(GenerationTaskType aTask, string aProviderModelId)
    {
        var vModel = objKit.AddModel(aTask, aProviderModelId);
        objCatalog.GetVisibleAsync(vModel.Id, Arg.Any<CancellationToken>()).Returns(vModel);
        return vModel;
    }

    private void Allow() => objSafetyPipeline.PreCheckAsync(Arg.Any<SafetyEvaluationContext>(), Arg.Any<CancellationToken>()).Returns(SafetyDecision.Allow());

    private void CapResult(bool aWouldExceed)
        => objSpendLedger.EvaluateAsync(Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(aCall => new SpendGuardResult(9.9m, 10m, 9.9m + aCall.Arg<decimal>(), aWouldExceed, aWouldExceed, aWouldExceed ? "Over the monthly cap." : null));

    /// <summary>REQ-FN-083: a prompt that trips a safety floor is refused with ZERO HTTP calls to Venice.</summary>
    [Theory]
    [InlineData(GenerationTaskType.TextToImage)]
    [InlineData(GenerationTaskType.TextToVideo)]
    public async Task TrippedSafetyFloorMakesNoVeniceCall(GenerationTaskType aTask)
    {
        var vModel = AddVisible(aTask, "test-model");
        objSafetyPipeline.PreCheckAsync(Arg.Any<SafetyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(SafetyDecision.Block(SafetyFloor.Csam, "floor", "Refused by a safety floor."));
        CapResult(false);

        var vRequest = new JobSubmitRequest { ModelId = vModel.Id, Task = aTask, ParamsJson = "{\"prompt\":\"forbidden\"}" };

        await Assert.ThrowsAsync<SafetyBlockedException>(() => CreateOrchestrator().SubmitAsync(vRequest, "user-1"));
        Assert.Empty(objKit.Handler.Requests);
        objBackgroundJobClient.DidNotReceive().Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    /// <summary>REQ-FN-085: a clip whose Venice quote would pass the cap is refused; the clip is never queued.</summary>
    [Fact]
    public async Task OverCapVideoIsNeverQueued()
    {
        var vModel = AddVisible(GenerationTaskType.TextToVideo, "test-t2v");
        objKit.Handler.Json("video/quote", "{\"quote\":0.5}");
        Allow();
        CapResult(true);

        var vRequest = new JobSubmitRequest { ModelId = vModel.Id, Task = GenerationTaskType.TextToVideo, ParamsJson = "{\"prompt\":\"waves\"}" };

        await Assert.ThrowsAsync<SpendCapExceededException>(() => CreateOrchestrator().SubmitAsync(vRequest, "user-1"));
        await objSpendLedger.Received(1).EvaluateAsync(0.5m, Arg.Any<CancellationToken>());
        Assert.Empty(objKit.Handler.To("video/queue"));
        Assert.All(objKit.Handler.Requests, aRequest => Assert.DoesNotContain("waves", aRequest.Body ?? string.Empty));
        objBackgroundJobClient.DidNotReceive().Create(Arg.Any<Job>(), Arg.Any<IState>());
        await objJobRepository.DidNotReceive().InsertAsync(Arg.Any<GenerationJob>(), Arg.Any<CancellationToken>());
    }

    /// <summary>REQ-FN-085: a picture whose listed price would pass the cap is refused; nothing is generated.</summary>
    [Fact]
    public async Task OverCapPictureIsNeverGenerated()
    {
        var vModel = AddVisible(GenerationTaskType.TextToImage, "test-image-model");
        objKit.Handler.Json("models", "{\"data\":[{\"id\":\"test-image-model\",\"type\":\"image\",\"model_spec\":{\"pricing\":{\"generation\":{\"usd\":0.2}}}}]}");
        Allow();
        CapResult(true);

        var vRequest = new JobSubmitRequest { ModelId = vModel.Id, Task = GenerationTaskType.TextToImage, ParamsJson = "{\"prompt\":\"cat\"}" };

        await Assert.ThrowsAsync<SpendCapExceededException>(() => CreateOrchestrator().SubmitAsync(vRequest, "user-1"));
        await objSpendLedger.Received(1).EvaluateAsync(0.2m, Arg.Any<CancellationToken>());
        Assert.Empty(objKit.Handler.To("image/generate"));
    }

    /// <summary>Decision 1A: the Venice price (not the registry estimate) is the job's cost, under the cap it is queued.</summary>
    [Fact]
    public async Task VenicePriceIsTheJobCost()
    {
        var vModel = AddVisible(GenerationTaskType.TextToVideo, "test-t2v");
        objKit.Handler.Json("video/quote", "{\"quote\":0.3}");
        Allow();
        CapResult(false);

        var vResponse = await CreateOrchestrator().SubmitAsync(
            new JobSubmitRequest { ModelId = vModel.Id, Task = GenerationTaskType.TextToVideo, ParamsJson = "{\"prompt\":\"w\"}" }, "user-1");

        Assert.Equal(0.3m, vResponse.CostEstimate);
        await objCostEstimator.DidNotReceive().EstimateAsync(Arg.Any<GenerationJob>(), Arg.Any<CancellationToken>());
        objBackgroundJobClient.Received(1).Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    /// <summary>A refused quote is recorded as a failed job and log row with Venice's own text, then surfaced.</summary>
    [Fact]
    public async Task RefusedQuoteIsRecordedWithVeniceText()
    {
        var vModel = AddVisible(GenerationTaskType.TextToVideo, "test-t2v");
        objKit.Handler.Json("video/quote", "{\"error\":\"Authentication failed\"}", System.Net.HttpStatusCode.Unauthorized);
        Allow();
        CapResult(false);

        var vError = await Assert.ThrowsAsync<VeniceApiException>(() => CreateOrchestrator().SubmitAsync(
            new JobSubmitRequest { ModelId = vModel.Id, Task = GenerationTaskType.TextToVideo, ParamsJson = "{\"prompt\":\"w\"}" }, "user-1"));

        Assert.Equal("Venice: Authentication failed (HTTP 401)", vError.Message);
        await objLogRepository.Received(1).InsertAsync(
            Arg.Is<GenerationLogEntry>(aEntry => aEntry.Outcome == "failed" && aEntry.FailureMessage == vError.Message),
            Arg.Any<CancellationToken>());
    }

    /// <summary>REQ-NFR-011: with a Venice provider configured, a ComfyUi row still resolves to ComfyUiProvider.</summary>
    [Fact]
    public void ComfyUiRowStillResolvesToComfyUiProvider()
    {
        var vComfy = new ComfyUiProvider(
            Substitute.For<IRegistryRepository>(),
            Substitute.For<IComfyWorkflowRepository>(),
            new ComfyGraphBuilder(),
            new ComfyDependencyScanner(new ComfyWorkflowParser()),
            new ComfyUiOptions(),
            Substitute.For<IHttpClientFactory>(),
            NullLogger<ComfyUiProvider>.Instance);
        var vResolver = new ProviderResolver(new IGenerationProvider[] { objKit.CreateProvider(), vComfy });

        Assert.Same(vComfy, vResolver.Resolve(new RegistryModel { Id = "comfy-wf", Task = GenerationTaskType.WorkflowRun, ProviderKind = ProviderKind.ComfyUi }));
        foreach (var vTask in new[] { GenerationTaskType.TextToImage, GenerationTaskType.ImageToImage, GenerationTaskType.Upscale, GenerationTaskType.TextToVideo, GenerationTaskType.ImageToVideo })
        {
            var vComfyRow = new RegistryModel { Id = "comfy", Task = vTask, ProviderKind = ProviderKind.ComfyUi };
            var vVeniceRow = new RegistryModel { Id = "venice", Task = vTask, ProviderKind = ProviderKind.Venice };
            Assert.Same(vComfy, vResolver.Resolve(vComfyRow));
            Assert.IsType<VeniceProvider>(vResolver.Resolve(vVeniceRow));
        }
    }

    /// <inheritdoc />
    public void Dispose() => objKit.Dispose();
}
