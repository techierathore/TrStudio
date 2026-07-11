using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Tests.Unit.Workspace;

/// <summary>
/// Unit tests for <see cref="PromptChatRepository"/> proving the per-user isolation invariant
/// (BRD-81 / FN-073): every session/message read and mutate SQL statement carries the
/// <c>"UserId"</c> owner filter, so no query can ever touch a row owned by a different user, and a
/// session delete removes its messages under the same filter. Guards against a future edit dropping
/// the owner predicate. A companion in-memory fake pins the behavioural contract the endpoints rely
/// on (foreign-user reads return null; a delete drops the messages; a user+assistant round-trip persists).
/// </summary>
public class PromptChatRepositoryOwnerFilterTests
{
    /// <summary>
    /// Every owner-scoped session/message statement filters on <c>"UserId" = @aUserId</c>.
    /// </summary>
    [Theory]
    [InlineData(PromptChatRepository.ListSessionsSql)]
    [InlineData(PromptChatRepository.GetSessionSql)]
    [InlineData(PromptChatRepository.RenameSessionSql)]
    [InlineData(PromptChatRepository.DeleteSessionMessagesSql)]
    [InlineData(PromptChatRepository.DeleteSessionSql)]
    [InlineData(PromptChatRepository.ListMessagesSql)]
    [InlineData(PromptChatRepository.TouchSessionSql)]
    public void OwnerScopedStatementsFilterByUserId(string aSql)
    {
        Assert.Contains("\"UserId\" = @aUserId", aSql);
    }

    /// <summary>
    /// The session insert statement persists the owning user id on the row.
    /// </summary>
    [Fact]
    public void CreateSessionPersistsUserId()
    {
        Assert.Contains("\"UserId\"", PromptChatRepository.CreateSessionSql);
        Assert.Contains("@UserId", PromptChatRepository.CreateSessionSql);
    }

    /// <summary>
    /// The message insert statement persists the owning user id on the row.
    /// </summary>
    [Fact]
    public void AddMessagePersistsUserId()
    {
        Assert.Contains("\"UserId\"", PromptChatRepository.AddMessageSql);
        Assert.Contains("@UserId", PromptChatRepository.AddMessageSql);
    }

    /// <summary>
    /// The single-session read binds both the session id and the caller's user id, so a user can
    /// never read another user's session even by guessing its id.
    /// </summary>
    [Fact]
    public void GetSessionBindsSessionIdAndUserId()
    {
        Assert.Contains("\"SessionId\" = @aSessionId", PromptChatRepository.GetSessionSql);
        Assert.Contains("\"UserId\" = @aUserId", PromptChatRepository.GetSessionSql);
    }

    /// <summary>
    /// A foreign user's <c>GetSession</c> returns null (owner filter, BRD-81).
    /// </summary>
    [Fact]
    public async Task GetSessionForeignUserReturnsNull()
    {
        var vRepository = new InMemoryPromptChatRepository();
        var vSessionId = Guid.NewGuid();
        await vRepository.CreateSessionAsync(NewSession(vSessionId, "owner"));

        var vForeign = await vRepository.GetSessionAsync(vSessionId, "intruder");

        Assert.Null(vForeign);
    }

    /// <summary>
    /// A foreign user's <c>DeleteSession</c> does not affect the owner's session or its messages.
    /// </summary>
    [Fact]
    public async Task DeleteSessionForeignUserDoesNotAffectOwnerRows()
    {
        var vRepository = new InMemoryPromptChatRepository();
        var vSessionId = Guid.NewGuid();
        await vRepository.CreateSessionAsync(NewSession(vSessionId, "owner"));
        await vRepository.AddMessageAsync(NewMessage(vSessionId, "owner", "user", "hi"));

        await vRepository.DeleteSessionAsync(vSessionId, "intruder");

        Assert.NotNull(await vRepository.GetSessionAsync(vSessionId, "owner"));
        Assert.Single(await vRepository.ListMessagesAsync(vSessionId, "owner"));
    }

    /// <summary>
    /// Deleting a session as its owner removes both the session and all of its messages.
    /// </summary>
    [Fact]
    public async Task DeleteSessionRemovesSessionAndMessages()
    {
        var vRepository = new InMemoryPromptChatRepository();
        var vSessionId = Guid.NewGuid();
        await vRepository.CreateSessionAsync(NewSession(vSessionId, "owner"));
        await vRepository.AddMessageAsync(NewMessage(vSessionId, "owner", "user", "hi"));
        await vRepository.AddMessageAsync(NewMessage(vSessionId, "owner", "assistant", "hello"));

        await vRepository.DeleteSessionAsync(vSessionId, "owner");

        Assert.Null(await vRepository.GetSessionAsync(vSessionId, "owner"));
        Assert.Empty(await vRepository.ListMessagesAsync(vSessionId, "owner"));
    }

    /// <summary>
    /// A user message followed by an assistant reply both persist and reload in creation order.
    /// </summary>
    [Fact]
    public async Task MessageRoundTripPersistsUserThenAssistant()
    {
        var vRepository = new InMemoryPromptChatRepository();
        var vSessionId = Guid.NewGuid();
        await vRepository.CreateSessionAsync(NewSession(vSessionId, "owner"));
        await vRepository.AddMessageAsync(NewMessage(vSessionId, "owner", "user", "write a prompt"));
        await vRepository.AddMessageAsync(NewMessage(vSessionId, "owner", "assistant", "here is one"));

        var vMessages = await vRepository.ListMessagesAsync(vSessionId, "owner");

        Assert.Equal(2, vMessages.Count);
        Assert.Equal("user", vMessages[0].Role);
        Assert.Equal("write a prompt", vMessages[0].Content);
        Assert.Equal("assistant", vMessages[1].Role);
        Assert.Equal("here is one", vMessages[1].Content);
    }

    private static PromptChatSession NewSession(Guid aSessionId, string aUserId) => new()
    {
        SessionId = aSessionId,
        UserId = aUserId,
        Title = "New chat",
        ModelId = "text-chat/echo",
        CreatedUtc = DateTime.UtcNow,
        UpdatedUtc = DateTime.UtcNow
    };

    private static PromptChatMessage NewMessage(Guid aSessionId, string aUserId, string aRole, string aContent) => new()
    {
        MessageId = Guid.NewGuid(),
        SessionId = aSessionId,
        UserId = aUserId,
        Role = aRole,
        Content = aContent,
        HasImage = false,
        CreatedUtc = DateTime.UtcNow
    };

    /// <summary>
    /// In-memory <see cref="IPromptChatRepository"/> honouring the same <c>"UserId"</c> owner-filter
    /// semantics as the Dapper repository, used to pin the behavioural contract without a live database.
    /// </summary>
    private sealed class InMemoryPromptChatRepository : IPromptChatRepository
    {
        private readonly List<PromptChatSession> objSessions = new();
        private readonly List<PromptChatMessage> objMessages = new();

        public Task CreateSessionAsync(PromptChatSession aSession, CancellationToken aCancellationToken = default)
        {
            objSessions.Add(aSession);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PromptChatSession>> ListSessionsAsync(string aUserId, CancellationToken aCancellationToken = default)
            => Task.FromResult<IReadOnlyList<PromptChatSession>>(
                objSessions.Where(aSession => aSession.UserId == aUserId)
                    .OrderByDescending(aSession => aSession.UpdatedUtc).ToList());

        public Task<PromptChatSession?> GetSessionAsync(Guid aSessionId, string aUserId, CancellationToken aCancellationToken = default)
            => Task.FromResult(objSessions.SingleOrDefault(
                aSession => aSession.SessionId == aSessionId && aSession.UserId == aUserId));

        public Task RenameSessionAsync(Guid aSessionId, string aUserId, string aTitle, CancellationToken aCancellationToken = default)
        {
            var vSession = objSessions.SingleOrDefault(aSession => aSession.SessionId == aSessionId && aSession.UserId == aUserId);
            if (vSession is not null)
            {
                vSession.Title = aTitle;
                vSession.UpdatedUtc = DateTime.UtcNow;
            }

            return Task.CompletedTask;
        }

        public Task DeleteSessionAsync(Guid aSessionId, string aUserId, CancellationToken aCancellationToken = default)
        {
            objMessages.RemoveAll(aMessage => aMessage.SessionId == aSessionId && aMessage.UserId == aUserId);
            objSessions.RemoveAll(aSession => aSession.SessionId == aSessionId && aSession.UserId == aUserId);
            return Task.CompletedTask;
        }

        public Task AddMessageAsync(PromptChatMessage aMessage, CancellationToken aCancellationToken = default)
        {
            objMessages.Add(aMessage);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PromptChatMessage>> ListMessagesAsync(Guid aSessionId, string aUserId, CancellationToken aCancellationToken = default)
            => Task.FromResult<IReadOnlyList<PromptChatMessage>>(
                objMessages.Where(aMessage => aMessage.SessionId == aSessionId && aMessage.UserId == aUserId)
                    .OrderBy(aMessage => aMessage.CreatedUtc).ToList());

        public Task TouchSessionAsync(Guid aSessionId, string aUserId, CancellationToken aCancellationToken = default)
        {
            var vSession = objSessions.SingleOrDefault(aSession => aSession.SessionId == aSessionId && aSession.UserId == aUserId);
            if (vSession is not null)
            {
                vSession.UpdatedUtc = DateTime.UtcNow;
            }

            return Task.CompletedTask;
        }
    }
}
