using TrStudio.Engine.Domain.Cost;
using TrStudio.Models.Entities;

namespace TrStudio.Tests.Unit.Cost;

/// <summary>
/// Unit tests for <see cref="CostEstimator"/> (REQ-FN-029/NFR-007): the pure pricing rule over a
/// registry cost block — serverless-seconds, per-call, local-free and the batch multiplier.
/// </summary>
public class CostEstimatorTests
{
    private readonly CostOptions objOptions = new()
    {
        RunPodPerSecondUsd = 0.001m,
        PerApiCallUsd = 0.05m
    };

    private CostEstimator CreateEstimator() => new(
        NSubstitute.Substitute.For<TrStudio.Models.Repositories.IRegistryRepository>(),
        objOptions);

    /// <summary>Serverless-seconds cost is estimated seconds times the per-second rate.</summary>
    [Fact]
    public void ServerlessSecondsPricedByDuration()
    {
        var vCost = CreateEstimator().EstimateFor(
            new ModelCost { Kind = "serverless-seconds", EstimateSecPerJob = 120 }, "{}");

        Assert.Equal(0.12m, vCost);
    }

    /// <summary>A batch/count param multiplies a serverless estimate.</summary>
    [Fact]
    public void BatchCountMultipliesEstimate()
    {
        var vCost = CreateEstimator().EstimateFor(
            new ModelCost { Kind = "serverless-seconds", EstimateSecPerJob = 100 }, "{\"batch\":4}");

        Assert.Equal(0.4m, vCost);
    }

    /// <summary>Per-image and api-call kinds are priced by the per-call rate.</summary>
    [Theory]
    [InlineData("per-image")]
    [InlineData("api-call")]
    public void PerCallKindsPricedByCallRate(string aKind)
    {
        var vCost = CreateEstimator().EstimateFor(new ModelCost { Kind = aKind }, "{\"count\":3}");

        Assert.Equal(0.15m, vCost);
    }

    /// <summary>Local-free and unknown/empty kinds always estimate zero.</summary>
    [Theory]
    [InlineData("local-free")]
    [InlineData("free")]
    [InlineData("")]
    [InlineData("mystery")]
    public void FreeKindsEstimateZero(string aKind)
    {
        var vCost = CreateEstimator().EstimateFor(new ModelCost { Kind = aKind, EstimateSecPerJob = 99 }, "{}");

        Assert.Equal(0m, vCost);
    }
}
