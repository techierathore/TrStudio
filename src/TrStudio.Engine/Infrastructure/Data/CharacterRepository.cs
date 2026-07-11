using Dapper;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="ICharacterRepository"/> over the <c>"Character"</c> table (FN-036).
/// EVERY read/write applies the <c>"UserId"</c> owner filter server-side so a creator can only see or
/// mutate their own characters (BRD-81). The enum columns round-trip as text; the reference-asset set
/// round-trips through a JSONB column. SQL is exposed as internal constants so the owner-filter
/// invariant is unit-testable without a live database.
/// </summary>
public class CharacterRepository : ICharacterRepository
{
    private const string SelectColumns =
        "\"CharacterId\", \"UserId\", \"Name\", \"Description\", \"Realism\", \"SafetyClass\", \"Seed\", " +
        "\"IdentityEmbedding\", \"LoraRef\", \"ReferenceAssetsJson\", \"IsRealPerson\", \"PhotorealOfRealPerson\", " +
        "\"DepictsMinor\", \"IsAdult\", \"CreatedUtc\", \"UpdatedUtc\"";

    /// <summary>Owner-filtered single-character read SQL (BRD-81).</summary>
    internal const string GetSql =
        "SELECT " + SelectColumns + " FROM \"Character\" WHERE \"CharacterId\" = @aCharacterId AND \"UserId\" = @aUserId";

    /// <summary>Owner-filtered list SQL (BRD-81), newest-first.</summary>
    internal const string ListSql =
        "SELECT " + SelectColumns + " FROM \"Character\" WHERE \"UserId\" = @aUserId ORDER BY \"CreatedUtc\" DESC";

    /// <summary>Insert SQL; the row carries its owner in <c>"UserId"</c>.</summary>
    internal const string InsertSql =
        "INSERT INTO \"Character\" (\"CharacterId\", \"UserId\", \"Name\", \"Description\", \"Realism\", \"SafetyClass\", " +
        "\"Seed\", \"IdentityEmbedding\", \"LoraRef\", \"ReferenceAssetsJson\", \"IsRealPerson\", \"PhotorealOfRealPerson\", " +
        "\"DepictsMinor\", \"IsAdult\", \"CreatedUtc\", \"UpdatedUtc\") " +
        "VALUES (@CharacterId, @UserId, @Name, @Description, @Realism, @SafetyClass, @Seed, @IdentityEmbedding, @LoraRef, " +
        "CAST(@ReferenceAssetsJson AS jsonb), @IsRealPerson, @PhotorealOfRealPerson, @DepictsMinor, @IsAdult, @CreatedUtc, @UpdatedUtc)";

    /// <summary>Owner-filtered update SQL (BRD-81); the locked identity (seed/references) is not touched.</summary>
    internal const string UpdateSql =
        "UPDATE \"Character\" SET \"Name\" = @Name, \"Description\" = @Description, \"Realism\" = @Realism, " +
        "\"SafetyClass\" = @SafetyClass, \"IdentityEmbedding\" = @IdentityEmbedding, \"LoraRef\" = @LoraRef, " +
        "\"IsRealPerson\" = @IsRealPerson, \"PhotorealOfRealPerson\" = @PhotorealOfRealPerson, " +
        "\"DepictsMinor\" = @DepictsMinor, \"IsAdult\" = @IsAdult, \"UpdatedUtc\" = @UpdatedUtc " +
        "WHERE \"CharacterId\" = @CharacterId AND \"UserId\" = @aUserId";

    /// <summary>Owner-filtered delete SQL (BRD-81).</summary>
    internal const string DeleteSql =
        "DELETE FROM \"Character\" WHERE \"CharacterId\" = @aCharacterId AND \"UserId\" = @aUserId";

    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public CharacterRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(Character aCharacter, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(InsertSql, new
        {
            aCharacter.CharacterId,
            aCharacter.UserId,
            aCharacter.Name,
            aCharacter.Description,
            Realism = aCharacter.Realism.ToString(),
            SafetyClass = aCharacter.SafetyClass.ToString(),
            aCharacter.Seed,
            aCharacter.IdentityEmbedding,
            aCharacter.LoraRef,
            aCharacter.ReferenceAssetsJson,
            aCharacter.IsRealPerson,
            aCharacter.PhotorealOfRealPerson,
            aCharacter.DepictsMinor,
            aCharacter.IsAdult,
            aCharacter.CreatedUtc,
            aCharacter.UpdatedUtc
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Character?> GetAsync(Guid aCharacterId, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRow = await vConnection.QuerySingleOrDefaultAsync<CharacterRow>(
            new CommandDefinition(GetSql, new { aCharacterId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vRow?.ToEntity();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Character>> ListAsync(string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRows = await vConnection.QueryAsync<CharacterRow>(
            new CommandDefinition(ListSql, new { aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vRows.Select(aRow => aRow.ToEntity()).ToList();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Character aCharacter, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(UpdateSql, new
        {
            aCharacter.CharacterId,
            aCharacter.Name,
            aCharacter.Description,
            Realism = aCharacter.Realism.ToString(),
            SafetyClass = aCharacter.SafetyClass.ToString(),
            aCharacter.IdentityEmbedding,
            aCharacter.LoraRef,
            aCharacter.IsRealPerson,
            aCharacter.PhotorealOfRealPerson,
            aCharacter.DepictsMinor,
            aCharacter.IsAdult,
            aCharacter.UpdatedUtc,
            aUserId
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid aCharacterId, string aUserId, CancellationToken aCancellationToken = default)
    {
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(
            new CommandDefinition(DeleteSql, new { aCharacterId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <summary>Flat Dapper row for <c>"Character"</c>; parses the enum text columns back to enums.</summary>
    private sealed class CharacterRow
    {
        public Guid CharacterId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Realism { get; set; } = string.Empty;
        public string SafetyClass { get; set; } = string.Empty;
        public long Seed { get; set; }
        public string? IdentityEmbedding { get; set; }
        public string? LoraRef { get; set; }
        public string ReferenceAssetsJson { get; set; } = "[]";
        public bool IsRealPerson { get; set; }
        public bool PhotorealOfRealPerson { get; set; }
        public bool DepictsMinor { get; set; }
        public bool IsAdult { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }

        public Character ToEntity() => new()
        {
            CharacterId = CharacterId,
            UserId = UserId,
            Name = Name,
            Description = Description,
            Realism = Enum.Parse<CharacterRealism>(Realism),
            SafetyClass = Enum.Parse<SafetyClass>(SafetyClass),
            Seed = Seed,
            IdentityEmbedding = IdentityEmbedding,
            LoraRef = LoraRef,
            ReferenceAssetsJson = ReferenceAssetsJson,
            IsRealPerson = IsRealPerson,
            PhotorealOfRealPerson = PhotorealOfRealPerson,
            DepictsMinor = DepictsMinor,
            IsAdult = IsAdult,
            CreatedUtc = CreatedUtc,
            UpdatedUtc = UpdatedUtc
        };
    }
}
