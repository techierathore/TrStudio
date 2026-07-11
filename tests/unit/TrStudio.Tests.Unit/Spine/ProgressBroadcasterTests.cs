using Microsoft.Extensions.Logging.Abstractions;
using TrStudio.Engine.Domain.Progress;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;

namespace TrStudio.Tests.Unit.Spine;

/// <summary>
/// Unit tests for <see cref="ProgressBroadcaster"/> (REQ-FN-005): published updates reach all
/// subscribers, and a faulting subscriber never breaks the publishing job or its peers.
/// </summary>
public class ProgressBroadcasterTests
{
    /// <summary>
    /// A published update is delivered to every registered subscriber with its payload intact.
    /// </summary>
    [Fact]
    public async Task PublishReachesAllSubscribers()
    {
        var vBroadcaster = new ProgressBroadcaster(NullLogger<ProgressBroadcaster>.Instance);
        var vReceived = new List<JobProgressUpdate>();
        vBroadcaster.Subscribe(aUpdate => { vReceived.Add(aUpdate); return Task.CompletedTask; });
        vBroadcaster.Subscribe(aUpdate => { vReceived.Add(aUpdate); return Task.CompletedTask; });

        var vUpdate = new JobProgressUpdate { JobId = Guid.NewGuid(), UserId = "user-1", State = JobState.Running, Progress = 42 };
        await vBroadcaster.PublishAsync(vUpdate);

        Assert.Equal(2, vReceived.Count);
        Assert.All(vReceived, aItem => Assert.Equal(42, aItem.Progress));
    }

    /// <summary>
    /// A subscriber that throws is swallowed (logged) and the remaining subscribers still
    /// receive the update — progress publication must never fail the job worker.
    /// </summary>
    [Fact]
    public async Task FaultingSubscriberDoesNotBreakOthers()
    {
        var vBroadcaster = new ProgressBroadcaster(NullLogger<ProgressBroadcaster>.Instance);
        var vDelivered = false;
        vBroadcaster.Subscribe(_ => throw new InvalidOperationException("boom"));
        vBroadcaster.Subscribe(_ => { vDelivered = true; return Task.CompletedTask; });

        await vBroadcaster.PublishAsync(new JobProgressUpdate { JobId = Guid.NewGuid(), UserId = "user-1" });

        Assert.True(vDelivered);
    }
}
