using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Cost;
using TrStudio.Engine.Domain.Jobs;
using TrStudio.Engine.Domain.Progress;
using TrStudio.Engine.Domain.Providers;
using TrStudio.Engine.Domain.Registry;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Tests.Unit.Spine;

/// <summary>
/// Unit tests for <see cref="JobOrchestrator"/> submit/cancel/reproduce behaviour
/// (REQ-FN-001/002/018): registry resolution, params-schema validation, Hangfire enqueue and
/// server-side owner filtering.
/// </summary>
public class JobOrchestratorTests
{
    private readonly IJobRepository objJobRepository = Substitute.For<IJobRepository>();
    private readonly IAssetRepository objAssetRepository = Substitute.For<IAssetRepository>();
    private readonly IGenerationLogRepository objLogRepository = Substitute.For<IGenerationLogRepository>();
    private readonly IModelCatalogService objModelCatalog = Substitute.For<IModelCatalogService>();
    private readonly IProviderResolver objProviderResolver = Substitute.For<IProviderResolver>();
    private readonly IBackgroundJobClient objBackgroundJobClient = Substitute.For<IBackgroundJobClient>();
    private readonly IProgressNotifier objProgressNotifier = Substitute.For<IProgressNotifier>();
    private readonly IGenerationProvider objProvider = Substitute.For<IGenerationProvider>();
    private readonly ICostEstimator objCostEstimator = Substitute.For<ICostEstimator>();
    private readonly ISpendLedger objSpendLedger = Substitute.For<ISpendLedger>();
    private readonly ISafetyPipeline objSafetyPipeline = Substitute.For<ISafetyPipeline>();
    private readonly CostOptions objCostOptions = new();

    private JobOrchestrator CreateOrchestrator()
    {
        objSpendLedger.EvaluateAsync(Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(new SpendGuardResult(0m, objCostOptions.MonthlyCapUsd, 0m, false, false, null));
        // Default: the safety pre-check allows (individual floor behaviour is covered by SafetyPipelineTests).
        objSafetyPipeline.PreCheckAsync(Arg.Any<SafetyEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(SafetyDecision.Allow());
        return new JobOrchestrator(
            objJobRepository,
            objAssetRepository,
            objLogRepository,
            objModelCatalog,
            objProviderResolver,
            objBackgroundJobClient,
            objProgressNotifier,
            objCostEstimator,
            objSpendLedger,
            objSafetyPipeline,
            objCostOptions,
            NullLogger<JobOrchestrator>.Instance);
    }

    private RegistryModel CreateStubModel() => new()
    {
        Id = "stub-echo",
        DisplayName = "Stub Echo",
        Task = GenerationTaskType.TextToImage,
        ProviderKind = ProviderKind.StubEcho,
        Tier = GenerationTier.Fast,
        Params = new List<ModelParamSpec>
        {
            new() { Key = "prompt", Type = "text", Required = true }
        },
        IsReady = true,
        IsEnabled = true
    };

    /// <summary>
    /// Submitting a valid request against a visible model inserts the job row, enqueues the
    /// Hangfire worker exactly once and returns the job id with the provider's cost estimate.
    /// </summary>
    [Fact]
    public async Task SubmitEnqueuesValidJob()
    {
        objModelCatalog.GetVisibleAsync("stub-echo", Arg.Any<CancellationToken>()).Returns(CreateStubModel());
        objProviderResolver.Resolve(Arg.Any<RegistryModel>()).Returns(objProvider);
        objProvider.EstimateCostAsync(Arg.Any<GenerationJob>(), Arg.Any<CancellationToken>()).Returns(0m);

        var vRequest = new JobSubmitRequest
        {
            ModelId = "stub-echo",
            Task = GenerationTaskType.TextToImage,
            Tier = GenerationTier.Fast,
            ParamsJson = "{\"prompt\":\"hello\"}"
        };

        var vResponse = await CreateOrchestrator().SubmitAsync(vRequest, "user-1");

        Assert.NotEqual(Guid.Empty, vResponse.JobId);
        await objJobRepository.Received(1).InsertAsync(
            Arg.Is<GenerationJob>(aJob => aJob.UserId == "user-1" && aJob.ModelId == "stub-echo"),
            Arg.Any<CancellationToken>());
        objBackgroundJobClient.Received(1).Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    /// <summary>
    /// Submitting against a model the catalog does not expose (unknown, disabled, or hidden by
    /// a disabled provider) throws KeyNotFoundException and never enqueues anything.
    /// </summary>
    [Fact]
    public async Task SubmitRejectsHiddenModel()
    {
        objModelCatalog.GetVisibleAsync("ghost", Arg.Any<CancellationToken>()).Returns((RegistryModel?)null);

        var vRequest = new JobSubmitRequest { ModelId = "ghost", Task = GenerationTaskType.TextToImage };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateOrchestrator().SubmitAsync(vRequest, "user-1"));
        objBackgroundJobClient.DidNotReceive().Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    /// <summary>
    /// Submitting without a parameter the model's declarative params schema marks required
    /// throws ArgumentException (REQ-FN-006 schema enforcement at the spine boundary).
    /// </summary>
    [Fact]
    public async Task SubmitRejectsMissingRequiredParam()
    {
        objModelCatalog.GetVisibleAsync("stub-echo", Arg.Any<CancellationToken>()).Returns(CreateStubModel());

        var vRequest = new JobSubmitRequest
        {
            ModelId = "stub-echo",
            Task = GenerationTaskType.TextToImage,
            ParamsJson = "{}"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => CreateOrchestrator().SubmitAsync(vRequest, "user-1"));
    }

    /// <summary>
    /// Submitting a task that differs from the registry row's declared task throws
    /// ArgumentException so the registry stays the single routing authority.
    /// </summary>
    [Fact]
    public async Task SubmitRejectsTaskMismatch()
    {
        objModelCatalog.GetVisibleAsync("stub-echo", Arg.Any<CancellationToken>()).Returns(CreateStubModel());

        var vRequest = new JobSubmitRequest
        {
            ModelId = "stub-echo",
            Task = GenerationTaskType.LipSync,
            ParamsJson = "{\"prompt\":\"hello\"}"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => CreateOrchestrator().SubmitAsync(vRequest, "user-1"));
    }

    /// <summary>
    /// Submitting a generation job against a text-chat LLM row is rejected with an ArgumentException
    /// (mapped to HTTP 400 by the spine endpoint) and never enqueues a worker — a text-chat model is
    /// served synchronously by PromptChatService and is never resolved to a generation provider
    /// (REQ-FN-074).
    /// </summary>
    [Fact]
    public async Task SubmitRejectsTextChatModel()
    {
        var vChatModel = new RegistryModel
        {
            Id = "gpt-chat",
            DisplayName = "GPT Chat",
            Task = GenerationTaskType.TextChat,
            ProviderKind = ProviderKind.TextLlm,
            Tier = GenerationTier.Fast,
            IsReady = true,
            IsEnabled = true
        };
        objModelCatalog.GetVisibleAsync("gpt-chat", Arg.Any<CancellationToken>()).Returns(vChatModel);

        var vRequest = new JobSubmitRequest
        {
            ModelId = "gpt-chat",
            Task = GenerationTaskType.TextChat,
            Tier = GenerationTier.Fast,
            ParamsJson = "{}"
        };

        var vException = await Assert.ThrowsAsync<ArgumentException>(
            () => CreateOrchestrator().SubmitAsync(vRequest, "user-1"));
        Assert.Contains("text-chat model and cannot run generation jobs", vException.Message);
        objProviderResolver.DidNotReceive().Resolve(Arg.Any<RegistryModel>());
        objBackgroundJobClient.DidNotReceive().Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    /// <summary>
    /// Cancelling a running job flips it to Cancelled through the owner-filtered repository
    /// call and publishes a Cancelled progress update.
    /// </summary>
    [Fact]
    public async Task CancelRunningJobSucceeds()
    {
        var vJobId = Guid.NewGuid();
        objJobRepository.GetAsync(vJobId, "user-1", Arg.Any<CancellationToken>())
            .Returns(new GenerationJob { JobId = vJobId, UserId = "user-1", State = JobState.Running, Progress = 40 });

        var vResult = await CreateOrchestrator().CancelAsync(vJobId, "user-1");

        Assert.True(vResult);
        await objJobRepository.Received(1).UpdateStateAsync(
            vJobId, "user-1", JobState.Cancelled, 40, Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await objProgressNotifier.Received(1).PublishAsync(
            Arg.Is<JobProgressUpdate>(aUpdate => aUpdate.State == JobState.Cancelled), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Cancelling a job that already reached a terminal state returns false and does not touch
    /// the row; cancelling a job the caller does not own returns null (owner filter, BRD-81).
    /// </summary>
    [Fact]
    public async Task CancelHonoursTerminalAndOwnership()
    {
        var vJobId = Guid.NewGuid();
        objJobRepository.GetAsync(vJobId, "user-1", Arg.Any<CancellationToken>())
            .Returns(new GenerationJob { JobId = vJobId, UserId = "user-1", State = JobState.Succeeded });
        objJobRepository.GetAsync(vJobId, "intruder", Arg.Any<CancellationToken>())
            .Returns((GenerationJob?)null);

        var vOrchestrator = CreateOrchestrator();

        Assert.False(await vOrchestrator.CancelAsync(vJobId, "user-1"));
        Assert.Null(await vOrchestrator.CancelAsync(vJobId, "intruder"));
    }

    /// <summary>
    /// Reproduce loads the owner-filtered generation-log row and re-submits a job with the same
    /// model, tier and params snapshot (REQ-FN-018 one-click reproduce).
    /// </summary>
    [Fact]
    public async Task ReproduceResubmitsFromLogRow()
    {
        var vLogId = Guid.NewGuid();
        objLogRepository.GetAsync(vLogId, "user-1", Arg.Any<CancellationToken>()).Returns(new GenerationLogEntry
        {
            LogId = vLogId,
            UserId = "user-1",
            ModelId = "stub-echo",
            Tier = GenerationTier.Fast,
            ParamsJson = "{\"prompt\":\"again\"}"
        });
        objModelCatalog.GetVisibleAsync("stub-echo", Arg.Any<CancellationToken>()).Returns(CreateStubModel());
        objProviderResolver.Resolve(Arg.Any<RegistryModel>()).Returns(objProvider);
        objProvider.EstimateCostAsync(Arg.Any<GenerationJob>(), Arg.Any<CancellationToken>()).Returns(0m);

        var vResponse = await CreateOrchestrator().ReproduceAsync(vLogId, "user-1");

        Assert.NotNull(vResponse);
        await objJobRepository.Received(1).InsertAsync(
            Arg.Is<GenerationJob>(aJob => aJob.ParamsJson == "{\"prompt\":\"again\"}" && aJob.ModelId == "stub-echo"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Reproduce returns null for a log row the caller does not own (owner filter, BRD-81).
    /// </summary>
    [Fact]
    public async Task ReproduceRejectsForeignLogRow()
    {
        objLogRepository.GetAsync(Arg.Any<Guid>(), "intruder", Arg.Any<CancellationToken>())
            .Returns((GenerationLogEntry?)null);

        var vResponse = await CreateOrchestrator().ReproduceAsync(Guid.NewGuid(), "intruder");

        Assert.Null(vResponse);
    }
}
