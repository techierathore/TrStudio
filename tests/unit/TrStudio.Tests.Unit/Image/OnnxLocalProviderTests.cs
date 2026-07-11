using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TrStudio.Engine.Domain.Image;
using TrStudio.Engine.Infrastructure.Storage;
using TrStudio.Engine.Providers;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Image;

/// <summary>
/// Unit tests for <see cref="OnnxLocalProvider"/> (REQ-FN-019/023): provider routing via
/// Supports, the deterministic synthetic-fallback success path persisting an owner-scoped asset,
/// seed reproducibility (same seed + params → identical bytes) and the graceful
/// no-model-installed failure carrying an actionable remediation hint (mirrors REQ-FN-005).
/// </summary>
public class OnnxLocalProviderTests
{
    private readonly IAssetStorage objAssetStorage = Substitute.For<IAssetStorage>();
    private readonly IAssetRepository objAssetRepository = Substitute.For<IAssetRepository>();
    private readonly ImageProcessor objImageProcessor = new();

    private OnnxLocalProvider CreateProvider(bool aSyntheticFallback)
    {
        objAssetStorage.SaveAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(aCall => Task.FromResult("/tmp/assets/" + aCall.Arg<string>()));

        var vOptions = Options.Create(new ImageProviderOptions
        {
            OnnxModelsRoot = null,
            SyntheticFallbackEnabled = aSyntheticFallback
        });

        return new OnnxLocalProvider(objImageProcessor, objAssetStorage, objAssetRepository, vOptions, NullLogger<OnnxLocalProvider>.Instance);
    }

    private static GenerationJob CreateJob(string aParamsJson, long aSeed) => new()
    {
        JobId = Guid.NewGuid(),
        UserId = "user-1",
        ModelId = "onnx-sdxl-local",
        Task = GenerationTaskType.TextToImage,
        Tier = GenerationTier.Fast,
        ParamsJson = aParamsJson,
        SeedUsed = aSeed,
        CreatedUtc = DateTime.UtcNow
    };

    private static async Task<TrStudio.Models.Providers.JobStatus> PollToTerminalAsync(OnnxLocalProvider aProvider, Guid aJobId)
    {
        var vDeadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < vDeadline)
        {
            var vStatus = await aProvider.GetStatusAsync(aJobId, CancellationToken.None);
            if (vStatus.State is JobState.Succeeded or JobState.Failed)
            {
                return vStatus;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("ONNX job did not reach a terminal state in time.");
    }

    /// <summary>
    /// Supports returns true only for registry rows with the OnnxLocal provider kind, so the
    /// resolver routes each model to exactly one provider.
    /// </summary>
    [Fact]
    public void SupportsOnlyOnnxLocalModels()
    {
        var vProvider = CreateProvider(aSyntheticFallback: true);

        Assert.True(vProvider.Supports(GenerationTaskType.TextToImage, new RegistryModel { ProviderKind = ProviderKind.OnnxLocal }));
        Assert.True(vProvider.Supports(GenerationTaskType.ImageToImage, new RegistryModel { ProviderKind = ProviderKind.OnnxLocal }));
        Assert.False(vProvider.Supports(GenerationTaskType.TextToImage, new RegistryModel { ProviderKind = ProviderKind.StubEcho }));
    }

    /// <summary>
    /// With synthetic fallback enabled (dev), a text-to-image job renders a deterministic image
    /// and persists an owner-scoped asset — the full provider contract without a real model.
    /// </summary>
    [Fact]
    public async Task SyntheticFallbackProducesOwnerScopedAsset()
    {
        var vProvider = CreateProvider(aSyntheticFallback: true);
        var vJob = CreateJob("{\"prompt\":\"a red cube\",\"width\":128,\"height\":128}", 100);

        var vJobId = await vProvider.SubmitAsync(vJob, CancellationToken.None);
        var vStatus = await PollToTerminalAsync(vProvider, vJobId);

        Assert.Equal(JobState.Succeeded, vStatus.State);
        var vResult = await vProvider.GetResultAsync(vJobId, CancellationToken.None);
        Assert.Equal("image", vResult.Kind);
        await objAssetRepository.Received(1).InsertAsync(
            Arg.Is<GeneratedAsset>(aAsset => aAsset.UserId == "user-1" && aAsset.JobId == vJobId),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The same seed + params always produce byte-identical output (REQ-FN-023 reproducibility).
    /// </summary>
    [Fact]
    public async Task SameSeedAndParamsReproduceIdenticalBytes()
    {
        var vProvider = CreateProvider(aSyntheticFallback: true);
        var vCaptured = new List<byte[]>();
        objAssetStorage
            .When(aStorage => aStorage.SaveAsync(Arg.Is<string>(aPath => aPath.EndsWith(".png") && !aPath.Contains("_thumb")), Arg.Any<byte[]>(), Arg.Any<CancellationToken>()))
            .Do(aCall => vCaptured.Add(aCall.Arg<byte[]>()));

        const string vParams = "{\"prompt\":\"identical\",\"width\":96,\"height\":96}";
        var vFirstId = await vProvider.SubmitAsync(CreateJob(vParams, 555), CancellationToken.None);
        await PollToTerminalAsync(vProvider, vFirstId);
        var vSecondId = await vProvider.SubmitAsync(CreateJob(vParams, 555), CancellationToken.None);
        await PollToTerminalAsync(vProvider, vSecondId);

        Assert.Equal(2, vCaptured.Count);
        Assert.Equal(vCaptured[0], vCaptured[1]);
    }

    /// <summary>
    /// With synthetic fallback disabled and no installed model, the job fails gracefully with an
    /// actionable remediation hint instead of crashing (REQ-FN-019, mirrors REQ-FN-005).
    /// </summary>
    [Fact]
    public async Task MissingModelWithoutFallbackFailsWithRemediationHint()
    {
        var vProvider = CreateProvider(aSyntheticFallback: false);
        var vJob = CreateJob("{\"prompt\":\"a red cube\"}", 1);

        var vJobId = await vProvider.SubmitAsync(vJob, CancellationToken.None);
        var vStatus = await PollToTerminalAsync(vProvider, vJobId);

        Assert.Equal(JobState.Failed, vStatus.State);
        Assert.Contains("No runnable ONNX model", vStatus.FailureMessage);
        Assert.False(string.IsNullOrWhiteSpace(vStatus.RemediationHint));
        await Assert.ThrowsAsync<InvalidOperationException>(() => vProvider.GetResultAsync(vJobId, CancellationToken.None));
    }
}
