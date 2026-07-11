using TrStudio.Models.Entities;

namespace TrStudio.Models.Repositories;

/// <summary>
/// Data access for persisted Prompt Studio chat sessions and messages (FN-073 / BRD-91). Every
/// read/write of user-owned rows takes the owning AppManager user id and applies the owner filter
/// server-side (per-user isolation, BRD-81).
/// </summary>
public interface IPromptChatRepository
{
    /// <summary>
    /// Inserts a new chat session. The session's <see cref="PromptChatSession.UserId"/> records ownership.
    /// </summary>
    /// <param name="aSession">The session to insert.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task CreateSessionAsync(PromptChatSession aSession, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists the user's sessions, most-recently-updated first.
    /// </summary>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The user's sessions.</returns>
    Task<IReadOnlyList<PromptChatSession>> ListSessionsAsync(string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Gets a session by id, owner-filtered.
    /// </summary>
    /// <param name="aSessionId">The session identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The session, or null when not found or not owned by <paramref name="aUserId"/>.</returns>
    Task<PromptChatSession?> GetSessionAsync(Guid aSessionId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Renames a session, owner-filtered, and bumps its <c>"UpdatedUtc"</c>.
    /// </summary>
    /// <param name="aSessionId">The session identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aTitle">The new title.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is updated.</returns>
    Task RenameSessionAsync(Guid aSessionId, string aUserId, string aTitle, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Deletes a session and all of its messages, both owner-filtered (BRD-81).
    /// </summary>
    /// <param name="aSessionId">The session identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the rows are removed.</returns>
    Task DeleteSessionAsync(Guid aSessionId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Appends a message to a session. The message's <see cref="PromptChatMessage.UserId"/> records ownership.
    /// </summary>
    /// <param name="aMessage">The message to insert.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is written.</returns>
    Task AddMessageAsync(PromptChatMessage aMessage, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Lists a session's messages in creation order, owner-filtered.
    /// </summary>
    /// <param name="aSessionId">The session identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The session's messages, oldest first.</returns>
    Task<IReadOnlyList<PromptChatMessage>> ListMessagesAsync(Guid aSessionId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Bumps a session's <c>"UpdatedUtc"</c> to now, owner-filtered (called after appending a turn).
    /// </summary>
    /// <param name="aSessionId">The session identifier.</param>
    /// <param name="aUserId">The owning AppManager user id (owner filter, BRD-81).</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>A task completing when the row is touched.</returns>
    Task TouchSessionAsync(Guid aSessionId, string aUserId, CancellationToken aCancellationToken = default);
}
