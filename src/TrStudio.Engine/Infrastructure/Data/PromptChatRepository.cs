using Dapper;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IPromptChatRepository"/> over the <c>"PromptChatSession"</c> and
/// <c>"PromptChatMessage"</c> tables (FN-073 / BRD-91). EVERY read/write applies the <c>"UserId"</c>
/// owner filter server-side so a creator can only ever see or mutate their own chats (BRD-81). The
/// SQL is exposed as internal constants so the owner-filter invariant is unit-tested without a DB.
/// </summary>
public class PromptChatRepository : IPromptChatRepository
{
    private const string SessionColumns =
        "\"SessionId\", \"UserId\", \"Title\", \"ModelId\", \"CreatedUtc\", \"UpdatedUtc\"";

    private const string MessageColumns =
        "\"MessageId\", \"SessionId\", \"UserId\", \"Role\", \"Content\", \"HasImage\", \"CreatedUtc\"";

    /// <summary>Session insert SQL; the row carries its owner in <c>"UserId"</c>.</summary>
    internal const string CreateSessionSql =
        "INSERT INTO \"PromptChatSession\" (\"SessionId\", \"UserId\", \"Title\", \"ModelId\", \"CreatedUtc\", \"UpdatedUtc\") " +
        "VALUES (@SessionId, @UserId, @Title, @ModelId, @CreatedUtc, @UpdatedUtc)";

    /// <summary>Owner-filtered session list SQL (BRD-81), most-recently-updated first.</summary>
    internal const string ListSessionsSql =
        "SELECT " + SessionColumns + " FROM \"PromptChatSession\" WHERE \"UserId\" = @aUserId " +
        "ORDER BY \"UpdatedUtc\" DESC";

    /// <summary>Owner-filtered single-session read SQL (BRD-81).</summary>
    internal const string GetSessionSql =
        "SELECT " + SessionColumns + " FROM \"PromptChatSession\" " +
        "WHERE \"SessionId\" = @aSessionId AND \"UserId\" = @aUserId";

    /// <summary>Owner-filtered session rename SQL (BRD-81); bumps <c>"UpdatedUtc"</c>.</summary>
    internal const string RenameSessionSql =
        "UPDATE \"PromptChatSession\" SET \"Title\" = @aTitle, \"UpdatedUtc\" = now() " +
        "WHERE \"SessionId\" = @aSessionId AND \"UserId\" = @aUserId";

    /// <summary>Owner-filtered delete of a session's messages (BRD-81).</summary>
    internal const string DeleteSessionMessagesSql =
        "DELETE FROM \"PromptChatMessage\" WHERE \"SessionId\" = @aSessionId AND \"UserId\" = @aUserId";

    /// <summary>Owner-filtered delete of the session row (BRD-81).</summary>
    internal const string DeleteSessionSql =
        "DELETE FROM \"PromptChatSession\" WHERE \"SessionId\" = @aSessionId AND \"UserId\" = @aUserId";

    /// <summary>Message insert SQL; the row carries its owner in <c>"UserId"</c>.</summary>
    internal const string AddMessageSql =
        "INSERT INTO \"PromptChatMessage\" (\"MessageId\", \"SessionId\", \"UserId\", \"Role\", \"Content\", \"HasImage\", \"CreatedUtc\") " +
        "VALUES (@MessageId, @SessionId, @UserId, @Role, @Content, @HasImage, @CreatedUtc)";

    /// <summary>Owner-filtered message list SQL (BRD-81), oldest first.</summary>
    internal const string ListMessagesSql =
        "SELECT " + MessageColumns + " FROM \"PromptChatMessage\" " +
        "WHERE \"SessionId\" = @aSessionId AND \"UserId\" = @aUserId ORDER BY \"CreatedUtc\" ASC";

    /// <summary>Owner-filtered session touch SQL (BRD-81); bumps <c>"UpdatedUtc"</c> to now.</summary>
    internal const string TouchSessionSql =
        "UPDATE \"PromptChatSession\" SET \"UpdatedUtc\" = now() " +
        "WHERE \"SessionId\" = @aSessionId AND \"UserId\" = @aUserId";

    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public PromptChatRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task CreateSessionAsync(PromptChatSession aSession, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(CreateSessionSql, new
        {
            aSession.SessionId,
            aSession.UserId,
            aSession.Title,
            aSession.ModelId,
            aSession.CreatedUtc,
            aSession.UpdatedUtc
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PromptChatSession>> ListSessionsAsync(string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vItems = await vConnection.QueryAsync<PromptChatSession>(
            new CommandDefinition(ListSessionsSql, new { aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vItems.ToList();
    }

    /// <inheritdoc />
    public async Task<PromptChatSession?> GetSessionAsync(Guid aSessionId, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.QuerySingleOrDefaultAsync<PromptChatSession>(
            new CommandDefinition(GetSessionSql, new { aSessionId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RenameSessionAsync(Guid aSessionId, string aUserId, string aTitle, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(
            new CommandDefinition(RenameSessionSql, new { aSessionId, aUserId, aTitle }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteSessionAsync(Guid aSessionId, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        // Owner-filtered on both statements so a foreign session's messages are never removed (BRD-81).
        await vConnection.ExecuteAsync(
            new CommandDefinition(DeleteSessionMessagesSql, new { aSessionId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        await vConnection.ExecuteAsync(
            new CommandDefinition(DeleteSessionSql, new { aSessionId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddMessageAsync(PromptChatMessage aMessage, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(AddMessageSql, new
        {
            aMessage.MessageId,
            aMessage.SessionId,
            aMessage.UserId,
            aMessage.Role,
            aMessage.Content,
            aMessage.HasImage,
            aMessage.CreatedUtc
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PromptChatMessage>> ListMessagesAsync(Guid aSessionId, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vItems = await vConnection.QueryAsync<PromptChatMessage>(
            new CommandDefinition(ListMessagesSql, new { aSessionId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vItems.ToList();
    }

    /// <inheritdoc />
    public async Task TouchSessionAsync(Guid aSessionId, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(
            new CommandDefinition(TouchSessionSql, new { aSessionId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }
}
