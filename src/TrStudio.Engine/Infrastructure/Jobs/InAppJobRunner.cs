using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrStudio.Engine.Domain.Jobs;
using TrStudio.Engine.Domain.Providers;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Jobs;

/// <summary>Settings of the in-app runner (not secret; defaults suit a desktop).</summary>
public sealed class InAppJobRunnerOptions
{
    /// <summary>How many jobs run at once.</summary>
    public int WorkerCount { get; set; } = 2;

    /// <summary>Retry delays after a failed attempt (two retries, as the Hangfire policy had).</summary>
    public IReadOnlyList<TimeSpan> RetryDelays { get; set; } = new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) };
}

/// <summary>
/// The in-app job runner (phase 3, ADR-018), replacing Hangfire and its PostgreSQL storage. Jobs are
/// rows of <c>GenerationJob</c>; <see cref="Enqueue"/> hands an id to a fixed number of workers that
/// run <see cref="GenerationJobWorker"/> in a scope each. At start, <see cref="RecoverAsync"/> queues
/// every job left Queued, resumes a Running Venice clip with its queue id, and fails any other
/// Running job with the reason (REQ-FN-091 to REQ-FN-095).
/// </summary>
public sealed class InAppJobRunner : IJobQueue, IAsyncDisposable
{
    private readonly IServiceProvider objServices;
    private readonly JobQueueStore objStore;
    private readonly InAppJobRunnerOptions objOptions;
    private readonly ILogger<InAppJobRunner> objLogger;
    private readonly Channel<Ticket> objChannel = Channel.CreateUnbounded<Ticket>(new UnboundedChannelOptions { SingleReader = false });
    private readonly CancellationTokenSource objStopping = new();
    private readonly List<Task> objWorkers = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> objHeld = new();
    private int objRunning;
    private int objDisposed;
    private int objMaxRunning;

    /// <summary>Creates the runner; <see cref="Start"/> starts its workers.</summary>
    /// <param name="aServices">The root service provider (a scope is created per job).</param>
    /// <param name="aStore">The job queue store.</param>
    /// <param name="aOptions">Runner settings.</param>
    /// <param name="aLogger">Logger.</param>
    public InAppJobRunner(IServiceProvider aServices, JobQueueStore aStore, InAppJobRunnerOptions aOptions, ILogger<InAppJobRunner> aLogger)
    {
        objServices = aServices;
        objStore = aStore;
        objOptions = aOptions;
        objLogger = aLogger;
    }

    /// <summary>Jobs running right now.</summary>
    public int RunningCount => Volatile.Read(ref objRunning);

    /// <summary>The most jobs seen running at one time since start (for the concurrency check).</summary>
    public int MaxRunningSeen => Volatile.Read(ref objMaxRunning);

    /// <summary>The configured worker count.</summary>
    public int WorkerCount => objOptions.WorkerCount;

    /// <summary>When the workers started.</summary>
    public DateTime? StartedUtc { get; private set; }

    /// <summary>Raised after a job's execution ends (success, failure or block), with its id.</summary>
    public event Action<Guid>? JobFinished;

    /// <inheritdoc />
    public void Enqueue(Guid aJobId, string aUserId, bool aResume = false)
    {
        // One job is held once: recovery at start never queues a job a submit already queued.
        if (objHeld.TryAdd(aJobId, 0) && !objChannel.Writer.TryWrite(new Ticket(aJobId, aUserId, aResume)))
        {
            objHeld.TryRemove(aJobId, out _);
        }
    }

    /// <summary>Starts the workers once.</summary>
    public void Start()
    {
        if (StartedUtc is not null)
        {
            return;
        }

        StartedUtc = DateTime.UtcNow;
        for (var vIndex = 0; vIndex < Math.Max(1, objOptions.WorkerCount); vIndex++)
        {
            objWorkers.Add(Task.Run(() => WorkAsync(objStopping.Token)));
        }

        objLogger.LogInformation("In-app job runner started with {WorkerCount} worker(s)", objOptions.WorkerCount);
    }

    /// <summary>
    /// Picks up the jobs left over from the last run: Queued rows run again; a Running row whose
    /// provider can resume (a Venice clip with its queue id) resumes polling; any other Running row
    /// is failed with <see cref="JobQueueStore.ClosedMidJobMessage"/>.
    /// </summary>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>How many were queued, resumed and failed.</returns>
    public async Task<(int Queued, int Resumed, int Failed)> RecoverAsync(CancellationToken aCancellationToken = default)
    {
        var vQueued = 0;
        var vResumed = 0;
        var vFailed = 0;
        foreach (var vTicket in await objStore.ListUnfinishedAsync(aCancellationToken).ConfigureAwait(false))
        {
            if (vTicket.State != nameof(Models.Enums.JobState.Running))
            {
                Enqueue(vTicket.JobId, vTicket.UserId);
                vQueued++;
                continue;
            }

            if (!string.IsNullOrEmpty(vTicket.ProviderRef) && await CanResumeAsync(vTicket, aCancellationToken).ConfigureAwait(false))
            {
                Enqueue(vTicket.JobId, vTicket.UserId, aResume: true);
                vResumed++;
                continue;
            }

            await objStore.FailClosedMidJobAsync(vTicket.JobId, aCancellationToken).ConfigureAwait(false);
            vFailed++;
        }

        objLogger.LogInformation("Recovered jobs at start: {Queued} queued, {Resumed} resumed, {Failed} failed", vQueued, vResumed, vFailed);
        return (vQueued, vResumed, vFailed);
    }

    /// <summary>Stops taking work; jobs still running stay Running and are recovered on the next start.</summary>
    /// <returns>A task.</returns>
    public async ValueTask DisposeAsync()
    {
        // The container disposes this instance once as itself and once as IJobQueue: only the first counts.
        if (Interlocked.Exchange(ref objDisposed, 1) == 1)
        {
            return;
        }

        objChannel.Writer.TryComplete();
        await objStopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await Task.WhenAll(objWorkers).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Shutting down: a job cut off here is recovered on the next start.
        }

        objStopping.Dispose();
    }

    private async Task<bool> CanResumeAsync(StoredJobTicket aTicket, CancellationToken aCancellationToken)
    {
        using var vScope = objServices.CreateScope();
        var vJobs = vScope.ServiceProvider.GetRequiredService<IJobRepository>();
        var vRegistry = vScope.ServiceProvider.GetRequiredService<IRegistryRepository>();
        var vResolver = vScope.ServiceProvider.GetRequiredService<IProviderResolver>();
        var vJob = await vJobs.GetAsync(aTicket.JobId, aTicket.UserId, aCancellationToken).ConfigureAwait(false);
        if (vJob is null)
        {
            return false;
        }

        var vModel = await vRegistry.GetAsync(vJob.ModelId, aCancellationToken).ConfigureAwait(false);
        return vModel is not null && vResolver.Resolve(vModel) is IResumableGenerationProvider;
    }

    private async Task WorkAsync(CancellationToken aStopping)
    {
        try
        {
            await foreach (var vTicket in objChannel.Reader.ReadAllAsync(aStopping).ConfigureAwait(false))
            {
                var vNow = Interlocked.Increment(ref objRunning);
                UpdateMax(vNow);
                try
                {
                    await RunWithRetriesAsync(vTicket, aStopping).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref objRunning);
                    objHeld.TryRemove(vTicket.JobId, out _);
                    JobFinished?.Invoke(vTicket.JobId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // App closing.
        }
    }

    private async Task RunWithRetriesAsync(Ticket aTicket, CancellationToken aStopping)
    {
        var vResume = aTicket.Resume;
        for (var vAttempt = 0; ; vAttempt++)
        {
            try
            {
                using var vScope = objServices.CreateScope();
                var vWorker = vScope.ServiceProvider.GetRequiredService<GenerationJobWorker>();
                await vWorker.ExecuteAsync(aTicket.JobId, aTicket.UserId, vResume, aStopping).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (aStopping.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception vException)
            {
                if (vAttempt >= objOptions.RetryDelays.Count)
                {
                    objLogger.LogWarning("Job {JobId} failed after {Attempts} attempt(s): {Message}", aTicket.JobId, vAttempt + 1, vException.Message);

                    // A provider that threw (rather than reporting a failure) must not leave the row Running.
                    await objStore.FailUnfinishedAsync(aTicket.JobId, Describe(vException), CancellationToken.None).ConfigureAwait(false);
                    return;
                }

                vResume = false;
                await Task.Delay(objOptions.RetryDelays[vAttempt], aStopping).ConfigureAwait(false);
            }
        }
    }

    private static string Describe(Exception aException)
    {
        for (var vInner = aException; vInner is not null; vInner = vInner.InnerException)
        {
            if (vInner is DllNotFoundException or EntryPointNotFoundException
                && vInner.Message.Contains("onnxruntime", StringComparison.OrdinalIgnoreCase))
            {
                return JobQueueStore.OnnxUnavailableMessage;
            }
        }

        return aException.Message;
    }

    private void UpdateMax(int aNow)
    {
        int vSeen;
        while (aNow > (vSeen = Volatile.Read(ref objMaxRunning)))
        {
            if (Interlocked.CompareExchange(ref objMaxRunning, aNow, vSeen) == vSeen)
            {
                return;
            }
        }
    }

    private readonly record struct Ticket(Guid JobId, string UserId, bool Resume);
}
