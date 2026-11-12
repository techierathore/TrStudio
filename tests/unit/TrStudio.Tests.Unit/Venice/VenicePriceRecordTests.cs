using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.Jobs;
using TrStudio.Engine.Domain.Progress;
using TrStudio.Engine.Domain.Providers;
using TrStudio.Engine.Domain.Venice;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Providers;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Tests.Unit.Venice;

/// <summary>
/// REQ-FN-082: a finished Venice generation's log row holds Venice's price (ActualCost) and its
/// price source, and the spend counts on the cloud-API channel of the ledger.
/// </summary>
public class VenicePriceRecordTests
{
    /// <summary>The price source is "venice-list" for pictures and "venice-quote" for clips; null for other providers.</summary>
    [Theory]
    [InlineData(ProviderKind.Venice, GenerationTaskType.TextToImage, VenicePriceSources.List)]
    [InlineData(ProviderKind.Venice, GenerationTaskType.Upscale, VenicePriceSources.List)]
    [InlineData(ProviderKind.Venice, GenerationTaskType.TextToVideo, VenicePriceSources.Quote)]
    [InlineData(ProviderKind.Venice, GenerationTaskType.ImageToVideo, VenicePriceSources.Quote)]
    [InlineData(ProviderKind.ComfyUi, GenerationTaskType.TextToImage, null)]
    public void PriceSourceByTask(ProviderKind aKind, GenerationTaskType aTask, string? aExpected)
        => Assert.Equal(aExpected, VenicePriceSources.For(aKind, aTask));

    /// <summary>The worker writes ActualCost = the Venice price and PriceSource on the succeeded log row.</summary>
    [Fact]
    public async Task SucceededVeniceRunLogsPriceAndSource()
    {
        var vJobs = Substitute.For<IJobRepository>();
        var vRegistry = Substitute.For<IRegistryRepository>();
        var vLog = Substitute.For<IGenerationLogRepository>();
        var vAssets = Substitute.For<IAssetRepository>();
        var vResolver = Substitute.For<IProviderResolver>();
        var vProvider = Substitute.For<IGenerationProvider>();
        var vLedger = Substitute.For<ISpendLedger>();
        var vSafety = Substitute.For<ISafetyPipeline>();

        var vJob = new GenerationJob
        {
            JobId = Guid.NewGuid(), UserId = "user-1", ModelId = "venice-pic", Task = GenerationTaskType.TextToImage,
            ParamsJson = "{\"prompt\":\"p\"}", State = JobState.Queued, CostEstimate = 0.05m, CreatedUtc = DateTime.UtcNow
        };
        var vModel = new RegistryModel { Id = "venice-pic", Task = GenerationTaskType.TextToImage, ProviderKind = ProviderKind.Venice };
        vJobs.GetAsync(vJob.JobId, "user-1", Arg.Any<CancellationToken>()).Returns(vJob);
        vRegistry.GetAsync("venice-pic", Arg.Any<CancellationToken>()).Returns(vModel);
        vResolver.Resolve(vModel).Returns(vProvider);
        vProvider.GetStatusAsync(vJob.JobId, Arg.Any<CancellationToken>()).Returns(new JobStatus(JobState.Succeeded, 100, null, null, null));
        vProvider.GetResultAsync(vJob.JobId, Arg.Any<CancellationToken>()).Returns(new AssetReference(Guid.NewGuid(), "x.png", "image"));
        vAssets.ListByJobAsync(vJob.JobId, "user-1", Arg.Any<CancellationToken>()).Returns(Array.Empty<GeneratedAsset>());
        vSafety.PostCheckAsync(Arg.Any<SafetyEvaluationContext>(), Arg.Any<SafetyContentSample>(), Arg.Any<CancellationToken>()).Returns(SafetyDecision.Allow());

        var vWorker = new GenerationJobWorker(
            vJobs, vRegistry, vLog, vAssets, vResolver, Substitute.For<IProgressNotifier>(), vLedger, vSafety,
            Substitute.For<IProvenanceStamper>(), NullLogger<GenerationJobWorker>.Instance);

        await vWorker.ExecuteAsync(vJob.JobId, "user-1", CancellationToken.None);

        await vLog.Received(1).InsertAsync(
            Arg.Is<GenerationLogEntry>(aEntry => aEntry.Outcome == "succeeded" && aEntry.ActualCost == 0.05m && aEntry.PriceSource == VenicePriceSources.List),
            Arg.Any<CancellationToken>());
        await vLedger.Received(1).RecordAsync("user-1", vJob.JobId, "venice-pic", SpendChannel.ClosedApi, 0.05m, Arg.Any<CancellationToken>());
    }

    /// <summary>The log SQL writes PriceSource and reads it back with the registry row's provider kind.</summary>
    [Fact]
    public void LogSqlCarriesPriceSourceAndProviderKind()
    {
        Assert.Contains("\"PriceSource\"", GenerationLogRepository.InsertSql);
        Assert.Contains("@PriceSource", GenerationLogRepository.InsertSql);
        Assert.Contains("l.\"PriceSource\"", GenerationLogRepository.SelectColumns);
        Assert.Contains("r.\"ProviderKind\"", GenerationLogRepository.SelectColumns);
        Assert.Contains("LEFT JOIN \"RegistryModel\" r", GenerationLogRepository.FromClause);
    }
}
