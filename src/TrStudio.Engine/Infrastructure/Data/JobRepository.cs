using Dapper;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IJobRepository"/> over the <c>"GenerationJob"</c> table.
/// Every user-owned query applies the <c>"UserId"</c> owner filter server-side (BRD-81).
/// </summary>
public class JobRepository : IJobRepository
{
    private const string SelectColumns =
        "\"GenerationJobId\" AS \"JobId\", \"UserId\", \"ModelId\", \"Task\", \"Tier\", " +
        "\"ParamsJson\", \"State\", \"Progress\", \"EtaSeconds\", \"CostEstimate\", \"ActualCost\", " +
        "\"FailureMessage\", \"RemediationHint\", \"CreatedUtc\", \"StartedUtc\", \"CompletedUtc\", " +
        "\"ProjectId\", \"SeedUsed\"";

    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public JobRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(GenerationJob aJob, CancellationToken aCancellationToken = default)
    {
        const string vSql =
            "INSERT INTO \"GenerationJob\" (\"GenerationJobId\", \"UserId\", \"ModelId\", \"Task\", \"Tier\", " +
            "\"ParamsJson\", \"State\", \"Progress\", \"EtaSeconds\", \"CostEstimate\", \"ActualCost\", " +
            "\"FailureMessage\", \"RemediationHint\", \"CreatedUtc\", \"StartedUtc\", \"CompletedUtc\", \"ProjectId\", \"SeedUsed\") " +
            "VALUES (@JobId, @UserId, @ModelId, @Task, @Tier, CAST(@ParamsJson AS jsonb), @State, @Progress, " +
            "@EtaSeconds, @CostEstimate, @ActualCost, @FailureMessage, @RemediationHint, @CreatedUtc, @StartedUtc, " +
            "@CompletedUtc, @ProjectId, @SeedUsed)";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(vSql, ToParams(aJob), cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GenerationJob?> GetAsync(Guid aJobId, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vSql = $"SELECT {SelectColumns} FROM \"GenerationJob\" WHERE \"GenerationJobId\" = @aJobId AND \"UserId\" = @aUserId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRow = await vConnection.QuerySingleOrDefaultAsync<JobRow>(
            new CommandDefinition(vSql, new { aJobId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vRow?.ToEntity();
    }

    /// <inheritdoc />
    public async Task<PagedResult<GenerationJob>> ListAsync(string aUserId, int aPage, int aPageSize, CancellationToken aCancellationToken = default)
    {
        var vPage = Math.Max(1, aPage);
        var vPageSize = Math.Clamp(aPageSize, 1, 200);
        var vSql =
            $"SELECT {SelectColumns} FROM \"GenerationJob\" WHERE \"UserId\" = @aUserId " +
            "ORDER BY \"CreatedUtc\" DESC LIMIT @aLimit OFFSET @aOffset; " +
            "SELECT COUNT(*) FROM \"GenerationJob\" WHERE \"UserId\" = @aUserId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        using var vMulti = await vConnection.QueryMultipleAsync(new CommandDefinition(
            vSql,
            new { aUserId, aLimit = vPageSize, aOffset = (vPage - 1) * vPageSize },
            cancellationToken: aCancellationToken)).ConfigureAwait(false);

        var vRows = (await vMulti.ReadAsync<JobRow>().ConfigureAwait(false)).Select(aRow => aRow.ToEntity()).ToList();
        var vTotal = await vMulti.ReadSingleAsync<int>().ConfigureAwait(false);
        return new PagedResult<GenerationJob> { Items = vRows, TotalCount = vTotal, Page = vPage, PageSize = vPageSize };
    }

    /// <inheritdoc />
    public async Task UpdateAsync(GenerationJob aJob, string aUserId, CancellationToken aCancellationToken = default)
    {
        const string vSql =
            "UPDATE \"GenerationJob\" SET \"State\" = @State, \"Progress\" = @Progress, \"EtaSeconds\" = @EtaSeconds, " +
            "\"CostEstimate\" = @CostEstimate, \"ActualCost\" = @ActualCost, \"FailureMessage\" = @FailureMessage, " +
            "\"RemediationHint\" = @RemediationHint, \"StartedUtc\" = @StartedUtc, \"CompletedUtc\" = @CompletedUtc, " +
            "\"SeedUsed\" = @SeedUsed WHERE \"GenerationJobId\" = @JobId AND \"UserId\" = @OwnerUserId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vParams = ToParams(aJob);
        vParams.Add("OwnerUserId", aUserId);
        await vConnection.ExecuteAsync(new CommandDefinition(vSql, vParams, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateStateAsync(Guid aJobId, string aUserId, JobState aState, int aProgress, string? aFailureMessage, CancellationToken aCancellationToken = default)
    {
        const string vSql =
            "UPDATE \"GenerationJob\" SET \"State\" = @aStateName, \"Progress\" = @aProgress, " +
            "\"FailureMessage\" = COALESCE(@aFailureMessage, \"FailureMessage\") " +
            "WHERE \"GenerationJobId\" = @aJobId AND \"UserId\" = @aUserId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(
            vSql,
            new { aJobId, aUserId, aStateName = aState.ToString(), aProgress, aFailureMessage },
            cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    private static DynamicParameters ToParams(GenerationJob aJob)
    {
        var vParams = new DynamicParameters();
        vParams.Add("JobId", aJob.JobId);
        vParams.Add("UserId", aJob.UserId);
        vParams.Add("ModelId", aJob.ModelId);
        vParams.Add("Task", aJob.Task.ToString());
        vParams.Add("Tier", aJob.Tier.ToString());
        vParams.Add("ParamsJson", aJob.ParamsJson);
        vParams.Add("State", aJob.State.ToString());
        vParams.Add("Progress", aJob.Progress);
        vParams.Add("EtaSeconds", aJob.Eta?.TotalSeconds);
        vParams.Add("CostEstimate", aJob.CostEstimate);
        vParams.Add("ActualCost", aJob.ActualCost);
        vParams.Add("FailureMessage", aJob.FailureMessage);
        vParams.Add("RemediationHint", aJob.RemediationHint);
        vParams.Add("CreatedUtc", aJob.CreatedUtc);
        vParams.Add("StartedUtc", aJob.StartedUtc);
        vParams.Add("CompletedUtc", aJob.CompletedUtc);
        vParams.Add("ProjectId", aJob.ProjectId);
        vParams.Add("SeedUsed", aJob.SeedUsed);
        return vParams;
    }

    /// <summary>
    /// Flat Dapper row for <c>"GenerationJob"</c>; converts the <c>EtaSeconds</c> column back
    /// to the entity's <see cref="TimeSpan"/> ETA.
    /// </summary>
    private sealed class JobRow
    {
        public Guid JobId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public string Task { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public string ParamsJson { get; set; } = "{}";
        public string State { get; set; } = string.Empty;
        public int Progress { get; set; }
        public double? EtaSeconds { get; set; }
        public decimal? CostEstimate { get; set; }
        public decimal? ActualCost { get; set; }
        public string? FailureMessage { get; set; }
        public string? RemediationHint { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? StartedUtc { get; set; }
        public DateTime? CompletedUtc { get; set; }
        public Guid? ProjectId { get; set; }
        public long? SeedUsed { get; set; }

        public GenerationJob ToEntity() => new()
        {
            JobId = JobId,
            UserId = UserId,
            ModelId = ModelId,
            Task = Enum.Parse<GenerationTaskType>(Task),
            Tier = Enum.Parse<GenerationTier>(Tier),
            ParamsJson = ParamsJson,
            State = Enum.Parse<JobState>(State),
            Progress = Progress,
            Eta = EtaSeconds.HasValue ? TimeSpan.FromSeconds(EtaSeconds.Value) : null,
            CostEstimate = CostEstimate,
            ActualCost = ActualCost,
            FailureMessage = FailureMessage,
            RemediationHint = RemediationHint,
            CreatedUtc = CreatedUtc,
            StartedUtc = StartedUtc,
            CompletedUtc = CompletedUtc,
            ProjectId = ProjectId,
            SeedUsed = SeedUsed
        };
    }
}
