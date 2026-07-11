using Dapper;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="ICharacterBibleRepository"/> over the <c>"CharacterBibleEntry"</c> table
/// (RAG-004). EVERY read/write applies the <c>"UserId"</c> owner filter (and the <c>"CharacterId"</c>
/// scope on list) server-side so a creator only ever touches their own persona's canon (BRD-81). SQL
/// is exposed as internal constants so the owner-filter invariant is unit-testable without a database.
/// </summary>
public class CharacterBibleRepository : ICharacterBibleRepository
{
    private const string SelectColumns =
        "\"EntryId\", \"CharacterId\", \"UserId\", \"Title\", \"Content\", \"CreatedUtc\", \"UpdatedUtc\"";

    /// <summary>Insert SQL; the row carries its owner in <c>"UserId"</c>.</summary>
    internal const string InsertSql =
        "INSERT INTO \"CharacterBibleEntry\" (\"EntryId\", \"CharacterId\", \"UserId\", \"Title\", \"Content\", \"CreatedUtc\", \"UpdatedUtc\") " +
        "VALUES (@EntryId, @CharacterId, @UserId, @Title, @Content, @CreatedUtc, @UpdatedUtc)";

    /// <summary>Owner + character filtered list SQL (BRD-81), newest-first.</summary>
    internal const string ListSql =
        "SELECT " + SelectColumns + " FROM \"CharacterBibleEntry\" " +
        "WHERE \"CharacterId\" = @aCharacterId AND \"UserId\" = @aUserId ORDER BY \"CreatedUtc\" DESC";

    /// <summary>Owner-filtered single-entry read SQL (BRD-81).</summary>
    internal const string GetSql =
        "SELECT " + SelectColumns + " FROM \"CharacterBibleEntry\" WHERE \"EntryId\" = @aEntryId AND \"UserId\" = @aUserId";

    /// <summary>Owner-filtered delete SQL (BRD-81).</summary>
    internal const string DeleteSql =
        "DELETE FROM \"CharacterBibleEntry\" WHERE \"EntryId\" = @aEntryId AND \"UserId\" = @aUserId";

    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public CharacterBibleRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(CharacterBibleEntry aEntry, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(InsertSql, new
        {
            aEntry.EntryId,
            aEntry.CharacterId,
            aEntry.UserId,
            aEntry.Title,
            aEntry.Content,
            aEntry.CreatedUtc,
            aEntry.UpdatedUtc
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CharacterBibleEntry>> ListAsync(Guid aCharacterId, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRows = await vConnection.QueryAsync<CharacterBibleEntry>(
            new CommandDefinition(ListSql, new { aCharacterId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vRows.ToList();
    }

    /// <inheritdoc />
    public async Task<CharacterBibleEntry?> GetAsync(Guid aEntryId, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.QuerySingleOrDefaultAsync<CharacterBibleEntry>(
            new CommandDefinition(GetSql, new { aEntryId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid aEntryId, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(
            new CommandDefinition(DeleteSql, new { aEntryId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }
}
