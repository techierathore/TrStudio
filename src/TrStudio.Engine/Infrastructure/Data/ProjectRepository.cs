using Dapper;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IProjectRepository"/> over the <c>"Project"</c> table (REQ-FN-026).
/// EVERY read/write applies the <c>"UserId"</c> owner filter server-side so a creator can only
/// ever see or mutate their own projects (BRD-81 / REQ-FN-027). The SQL is exposed as internal
/// constants so the owner-filter invariant is unit-tested without a live database.
/// </summary>
public class ProjectRepository : IProjectRepository
{
    private const string SelectColumns =
        "\"ProjectId\", \"UserId\", \"Name\", \"Description\", \"IsArchived\", \"CreatedUtc\", \"UpdatedUtc\"";

    /// <summary>Owner-filtered single-project read SQL (BRD-81).</summary>
    internal const string GetSql =
        "SELECT " + SelectColumns + " FROM \"Project\" WHERE \"ProjectId\" = @aProjectId AND \"UserId\" = @aUserId";

    /// <summary>Owner-filtered list SQL (BRD-81); archived rows are excluded unless requested.</summary>
    internal const string ListSql =
        "SELECT " + SelectColumns + " FROM \"Project\" WHERE \"UserId\" = @aUserId " +
        "AND (@aIncludeArchived OR \"IsArchived\" = FALSE) ORDER BY \"UpdatedUtc\" DESC";

    /// <summary>Insert SQL; the row carries its owner in <c>"UserId"</c>.</summary>
    internal const string InsertSql =
        "INSERT INTO \"Project\" (\"ProjectId\", \"UserId\", \"Name\", \"Description\", \"IsArchived\", \"CreatedUtc\", \"UpdatedUtc\") " +
        "VALUES (@ProjectId, @UserId, @Name, @Description, @IsArchived, @CreatedUtc, @UpdatedUtc)";

    /// <summary>Owner-filtered update SQL (BRD-81).</summary>
    internal const string UpdateSql =
        "UPDATE \"Project\" SET \"Name\" = @Name, \"Description\" = @Description, \"IsArchived\" = @IsArchived, " +
        "\"UpdatedUtc\" = @UpdatedUtc WHERE \"ProjectId\" = @ProjectId AND \"UserId\" = @aUserId";

    /// <summary>Owner-filtered archive (soft-delete) SQL (BRD-81).</summary>
    internal const string ArchiveSql =
        "UPDATE \"Project\" SET \"IsArchived\" = TRUE, \"UpdatedUtc\" = now() " +
        "WHERE \"ProjectId\" = @aProjectId AND \"UserId\" = @aUserId";

    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public ProjectRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(Project aProject, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(InsertSql, new
        {
            aProject.ProjectId,
            aProject.UserId,
            aProject.Name,
            aProject.Description,
            aProject.IsArchived,
            aProject.CreatedUtc,
            aProject.UpdatedUtc
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Project?> GetAsync(Guid aProjectId, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.QuerySingleOrDefaultAsync<Project>(
            new CommandDefinition(GetSql, new { aProjectId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Project>> ListAsync(string aUserId, bool aIncludeArchived, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vItems = await vConnection.QueryAsync<Project>(
            new CommandDefinition(ListSql, new { aUserId, aIncludeArchived }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vItems.ToList();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Project aProject, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(UpdateSql, new
        {
            aProject.ProjectId,
            aProject.Name,
            aProject.Description,
            aProject.IsArchived,
            aProject.UpdatedUtc,
            aUserId
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ArchiveAsync(Guid aProjectId, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(
            new CommandDefinition(ArchiveSql, new { aProjectId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }
}
