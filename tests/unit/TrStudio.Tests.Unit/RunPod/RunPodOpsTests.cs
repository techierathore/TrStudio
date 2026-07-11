using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TrStudio.Engine.Domain.RunPod;

namespace TrStudio.Tests.Unit.RunPod;

/// <summary>
/// Unit tests for <see cref="RunPodOps"/> (REQ-FN-028/BRD-12): the graceful "not configured"
/// status, the configured status derived from endpoint health, and the start / scale-to-zero /
/// auto idle-stop toggle operations.
/// </summary>
public class RunPodOpsTests
{
    private readonly IRunPodClient objClient = Substitute.For<IRunPodClient>();

    private RunPodOps CreateOps(RunPodOptions aOptions) => new(aOptions, objClient, NullLogger<RunPodOps>.Instance);

    private static RunPodOptions Configured() => new() { ApiKey = "rp-key", EndpointId = "ep-1", AutoIdleStop = true };

    /// <summary>With no key configured, status is a clean not-configured payload and the client is untouched.</summary>
    [Fact]
    public async Task StatusNotConfiguredWhenNoKey()
    {
        var vStatus = await CreateOps(new RunPodOptions()).GetStatusAsync();

        Assert.False(vStatus.Configured);
        Assert.Equal("not-configured", vStatus.WorkerState);
        Assert.Contains("RunPod:ApiKey", vStatus.Message);
        await objClient.DidNotReceive().GetHealthAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Start on an unconfigured endpoint degrades gracefully and does not scale anything.</summary>
    [Fact]
    public async Task StartNotConfiguredDoesNotCallClient()
    {
        var vStatus = await CreateOps(new RunPodOptions()).StartAsync();

        Assert.False(vStatus.Configured);
        await objClient.DidNotReceive().SetMinWorkersAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A configured cold endpoint reports the cold-start flag and a "cold" worker state.</summary>
    [Fact]
    public async Task StatusConfiguredReportsColdStart()
    {
        objClient.GetHealthAsync(Arg.Any<CancellationToken>()).Returns(new RunPodHealth(0, 0, 0, true));

        var vStatus = await CreateOps(Configured()).GetStatusAsync();

        Assert.True(vStatus.Configured);
        Assert.True(vStatus.ColdStart);
        Assert.Equal("cold", vStatus.WorkerState);
        Assert.Equal("ep-1", vStatus.EndpointId);
    }

    /// <summary>Start scales the endpoint up to one worker; stop scales it to zero.</summary>
    [Fact]
    public async Task StartAndStopScaleWorkers()
    {
        objClient.GetHealthAsync(Arg.Any<CancellationToken>()).Returns(new RunPodHealth(1, 0, 0, false));
        var vOps = CreateOps(Configured());

        await vOps.StartAsync();
        await vOps.StopAsync();

        await objClient.Received(1).SetMinWorkersAsync(1, Arg.Any<CancellationToken>());
        await objClient.Received(1).SetMinWorkersAsync(0, Arg.Any<CancellationToken>());
    }

    /// <summary>The auto idle-stop toggle mutates the shared options and is reflected in the status.</summary>
    [Fact]
    public async Task ToggleAutoIdleStopUpdatesOptions()
    {
        var vOptions = Configured();
        objClient.GetHealthAsync(Arg.Any<CancellationToken>()).Returns(new RunPodHealth(1, 0, 0, false));

        var vStatus = await CreateOps(vOptions).SetAutoIdleStopAsync(false);

        Assert.False(vOptions.AutoIdleStop);
        Assert.False(vStatus.AutoIdleStop);
    }
}
