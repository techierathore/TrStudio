using Dapper;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IComfyWorkflowRepository"/> over the <c>"ComfyWorkflow"</c> and
/// <c>"ComfyWorkflowVersion"</c> tables (FN-043..050). Workflow reads are owner-filtered by the
/// importing admin's user id (BRD-81); the graph JSON round-trips through a JSONB column.
/// </summary>
public class ComfyWorkflowRepository : IComfyWorkflowRepository
{
    private const string WorkflowColumns =
        "\"ComfyWorkflowId\" AS \"WorkflowId\", \"OwnerUserId\", \"Name\", \"CreatedUtc\", \"UpdatedUtc\"";

    private const string VersionColumns =
        "\"ComfyWorkflowVersionId\" AS \"VersionId\", \"ComfyWorkflowId\" AS \"WorkflowId\", \"VersionNumber\", " +
        "\"GraphJson\", \"MappedParamsJson\", \"SmokeStatus\", \"SmokeMessage\", \"CreatedUtc\"";

    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public ComfyWorkflowRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertWorkflowAsync(ComfyWorkflow aWorkflow, CancellationToken aCancellationToken = default)
    {
        const string vSql =
            "INSERT INTO \"ComfyWorkflow\" (\"ComfyWorkflowId\", \"OwnerUserId\", \"Name\", \"CreatedUtc\", \"UpdatedUtc\") " +
            "VALUES (@WorkflowId, @OwnerUserId, @Name, @CreatedUtc, @UpdatedUtc)";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(vSql, new
        {
            aWorkflow.WorkflowId,
            aWorkflow.OwnerUserId,
            aWorkflow.Name,
            aWorkflow.CreatedUtc,
            aWorkflow.UpdatedUtc
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ComfyWorkflow?> GetWorkflowAsync(Guid aWorkflowId, string aOwnerUserId, CancellationToken aCancellationToken = default)
    {
        var vSql = $"SELECT {WorkflowColumns} FROM \"ComfyWorkflow\" " +
                   "WHERE \"ComfyWorkflowId\" = @aWorkflowId AND \"OwnerUserId\" = @aOwnerUserId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.QuerySingleOrDefaultAsync<ComfyWorkflow>(
            new CommandDefinition(vSql, new { aWorkflowId, aOwnerUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ComfyWorkflow>> ListWorkflowsAsync(string aOwnerUserId, CancellationToken aCancellationToken = default)
    {
        var vSql = $"SELECT {WorkflowColumns} FROM \"ComfyWorkflow\" " +
                   "WHERE \"OwnerUserId\" = @aOwnerUserId ORDER BY \"CreatedUtc\" DESC";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRows = await vConnection.QueryAsync<ComfyWorkflow>(
            new CommandDefinition(vSql, new { aOwnerUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vRows.ToList();
    }

    /// <inheritdoc />
    public async Task InsertVersionAsync(ComfyWorkflowVersion aVersion, CancellationToken aCancellationToken = default)
    {
        const string vSql =
            "INSERT INTO \"ComfyWorkflowVersion\" (\"ComfyWorkflowVersionId\", \"ComfyWorkflowId\", \"VersionNumber\", " +
            "\"GraphJson\", \"MappedParamsJson\", \"SmokeStatus\", \"SmokeMessage\", \"CreatedUtc\") " +
            "VALUES (@VersionId, @WorkflowId, @VersionNumber, CAST(@GraphJson AS jsonb), " +
            "CAST(@MappedParamsJson AS jsonb), @SmokeStatus, @SmokeMessage, @CreatedUtc)";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(vSql, new
        {
            aVersion.VersionId,
            aVersion.WorkflowId,
            aVersion.VersionNumber,
            aVersion.GraphJson,
            aVersion.MappedParamsJson,
            aVersion.SmokeStatus,
            aVersion.SmokeMessage,
            aVersion.CreatedUtc
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ComfyWorkflowVersion?> GetVersionAsync(Guid aVersionId, CancellationToken aCancellationToken = default)
    {
        var vSql = $"SELECT {VersionColumns} FROM \"ComfyWorkflowVersion\" WHERE \"ComfyWorkflowVersionId\" = @aVersionId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.QuerySingleOrDefaultAsync<ComfyWorkflowVersion>(
            new CommandDefinition(vSql, new { aVersionId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ComfyWorkflowVersion>> ListVersionsAsync(Guid aWorkflowId, CancellationToken aCancellationToken = default)
    {
        var vSql = $"SELECT {VersionColumns} FROM \"ComfyWorkflowVersion\" " +
                   "WHERE \"ComfyWorkflowId\" = @aWorkflowId ORDER BY \"VersionNumber\" DESC";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRows = await vConnection.QueryAsync<ComfyWorkflowVersion>(
            new CommandDefinition(vSql, new { aWorkflowId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vRows.ToList();
    }

    /// <inheritdoc />
    public async Task<int> NextVersionNumberAsync(Guid aWorkflowId, CancellationToken aCancellationToken = default)
    {
        const string vSql =
            "SELECT COALESCE(MAX(\"VersionNumber\"), 0) + 1 FROM \"ComfyWorkflowVersion\" WHERE \"ComfyWorkflowId\" = @aWorkflowId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.ExecuteScalarAsync<int>(
            new CommandDefinition(vSql, new { aWorkflowId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateSmokeStatusAsync(Guid aVersionId, string aStatus, string? aMessage, CancellationToken aCancellationToken = default)
    {
        const string vSql =
            "UPDATE \"ComfyWorkflowVersion\" SET \"SmokeStatus\" = @aStatus, \"SmokeMessage\" = @aMessage " +
            "WHERE \"ComfyWorkflowVersionId\" = @aVersionId";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(
            new CommandDefinition(vSql, new { aVersionId, aStatus, aMessage }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }
}
