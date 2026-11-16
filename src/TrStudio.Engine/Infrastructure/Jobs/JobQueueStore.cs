using Dapper;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Models.Enums;

namespace TrStudio.Engine.Infrastructure.Jobs;

/// <summary>A queued or running job found at start-up.</summary>
/// <param name="JobId">The job id.</param>
/// <param name="UserId">The owning user.</param>
/// <param name="State">Its stored state.</param>
/// <param name="ProviderRef">The provider's reference (Venice queue id), when stored.</param>
public sealed record StoredJobTicket(Guid JobId, string UserId, string State, string? ProviderRef);

/// <summary>
/// The queue-side reads and writes on <c>GenerationJob</c> for the in-app runner (phase 3, ADR-018):
/// jobs left Queued or Running across all users at start-up, the provider reference, the failure of
/// an abandoned job, and the queue counts the ops screen shows.
/// </summary>
public class JobQueueStore
{
    /// <summary>The message stored on a job that was running when the app closed and cannot resume.</summary>
    public const string ClosedMidJobMessage = "TrStudio was closed while this job was running. Run it again.";

    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>Creates the store.</summary>
    /// <param name="aConnectionFactory">The local database.</param>
    public JobQueueStore(IDbConnectionFactory aConnectionFactory) => objConnectionFactory = aConnectionFactory;

    /// <summary>Lists every job left Queued or Running, oldest first.</summary>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>The tickets.</returns>
    public async Task<IReadOnlyList<StoredJobTicket>> ListUnfinishedAsync(CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRows = await vConnection.QueryAsync<StoredJobTicket>(new CommandDefinition(
            "SELECT \"GenerationJobId\" AS JobId, \"UserId\", \"State\", \"ProviderRef\" FROM \"GenerationJob\" " +
            "WHERE \"State\" IN (@aQueued, @aPending, @aRunning) ORDER BY \"CreatedUtc\"",
            new { aQueued = JobState.Queued.ToString(), aPending = JobState.Pending.ToString(), aRunning = JobState.Running.ToString() },
            cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vRows.ToList();
    }

    /// <summary>Stores the provider's reference for a job.</summary>
    /// <param name="aJobId">The job id.</param>
    /// <param name="aProviderRef">The reference.</param>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SetProviderRefAsync(Guid aJobId, string aProviderRef, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(
            "UPDATE \"GenerationJob\" SET \"ProviderRef\" = @aProviderRef WHERE \"GenerationJobId\" = @aJobId",
            new { aJobId, aProviderRef }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <summary>Marks a job that was running at close as Failed with the reason.</summary>
    /// <param name="aJobId">The job id.</param>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task FailClosedMidJobAsync(Guid aJobId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(
            "UPDATE \"GenerationJob\" SET \"State\" = @aFailed, \"FailureMessage\" = @aMessage, " +
            "\"RemediationHint\" = @aHint, \"CompletedUtc\" = now() WHERE \"GenerationJobId\" = @aJobId",
            new
            {
                aJobId,
                aFailed = JobState.Failed.ToString(),
                aMessage = ClosedMidJobMessage,
                aHint = "Submit the job again from its studio."
            },
            cancellationToken: aCancellationToken)).ConfigureAwait(false);
        await LogFailureAsync(vConnection, aJobId, ClosedMidJobMessage, aCancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the job's "failed" generation-log row, as the worker does for a failure it sees, so the
    /// reason shows in Account's Recent jobs (owner run 16 Nov 2026: a job failed at start never did).
    /// One row per job: nothing is written when the job already has a log row.
    /// </summary>
    private static Task LogFailureAsync(System.Data.IDbConnection aConnection, Guid aJobId, string aMessage, CancellationToken aCancellationToken)
        => aConnection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO \"GenerationLog\" (\"GenerationLogId\", \"GenerationJobId\", \"UserId\", \"ModelId\", \"Tier\", \"ParamsJson\", " +
            "\"CostEstimate\", \"ActualCost\", \"DurationMs\", \"Outcome\", \"FailureMessage\") " +
            "SELECT @aLogId, \"GenerationJobId\", \"UserId\", \"ModelId\", \"Tier\", \"ParamsJson\", \"CostEstimate\", \"ActualCost\", NULL, 'failed', @aMessage " +
            "FROM \"GenerationJob\" WHERE \"GenerationJobId\" = @aJobId " +
            "AND NOT EXISTS (SELECT 1 FROM \"GenerationLog\" WHERE \"GenerationJobId\" = @aJobId)",
            new { aLogId = Guid.NewGuid(), aJobId, aMessage },
            cancellationToken: aCancellationToken));

    /// <summary>The message stored when this build cannot load ONNX Runtime (the Mac app; owner decision 10).</summary>
    public const string OnnxUnavailableMessage =
        "This copy of TrStudio cannot run local ONNX models on this computer (ONNX Runtime is not included in the Mac app). Use a ComfyUI, RunPod or Venice model.";

    /// <summary>Marks a job Failed with a reason when its execution ended with an error and the row is not finished.</summary>
    /// <param name="aJobId">The job id.</param>
    /// <param name="aMessage">The reason shown on the job.</param>
    /// <param name="aCancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task FailUnfinishedAsync(Guid aJobId, string aMessage, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(
            "UPDATE \"GenerationJob\" SET \"State\" = @aFailed, \"FailureMessage\" = @aMessage, \"CompletedUtc\" = now() " +
            "WHERE \"GenerationJobId\" = @aJobId AND \"State\" IN (@aQueued, @aPending, @aRunning)",
            new
            {
                aJobId,
                aMessage,
                aFailed = JobState.Failed.ToString(),
                aQueued = JobState.Queued.ToString(),
                aPending = JobState.Pending.ToString(),
                aRunning = JobState.Running.ToString()
            },
            cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <summary>Counts jobs by state for the ops screen.</summary>
    /// <returns>Queued (with Pending) and Running counts.</returns>
    public (long Queued, long Running) CountQueuedAndRunning()
    {
        using var vConnection = objConnectionFactory.CreateOpenConnectionAsync().GetAwaiter().GetResult();
        var vCounts = vConnection.QuerySingle<(long Queued, long Running)>(
            "SELECT " +
            "COALESCE(SUM(CASE WHEN \"State\" IN (@aQueued, @aPending) THEN 1 ELSE 0 END), 0) AS Queued, " +
            "COALESCE(SUM(CASE WHEN \"State\" = @aRunning THEN 1 ELSE 0 END), 0) AS Running " +
            "FROM \"GenerationJob\"",
            new { aQueued = JobState.Queued.ToString(), aPending = JobState.Pending.ToString(), aRunning = JobState.Running.ToString() });
        return vCounts;
    }
}
