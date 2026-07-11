using Dapper;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IVoiceCloneRepository"/> over the <c>"VoiceClone"</c> table (script 0800,
/// FN-032). Every read/write is owner-filtered by the AppManager user id (BRD-81); the parameterised
/// SQL quotes all PascalCase identifiers.
/// </summary>
public class VoiceCloneRepository : IVoiceCloneRepository
{
    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public VoiceCloneRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(VoiceClone aClone, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            INSERT INTO "VoiceClone"
                ("CloneId", "UserId", "VoiceName", "Language", "IsRealPerson", "SubjectReference", "Status", "CreatedUtc")
            VALUES (@aCloneId, @aUserId, @aVoiceName, @aLanguage, @aIsRealPerson, @aSubjectReference, @aStatus, @aCreatedUtc)
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(
            Sql,
            new
            {
                aCloneId = aClone.CloneId,
                aUserId = aClone.UserId,
                aVoiceName = aClone.VoiceName,
                aLanguage = aClone.Language,
                aIsRealPerson = aClone.IsRealPerson,
                aSubjectReference = aClone.SubjectReference,
                aStatus = aClone.Status,
                aCreatedUtc = aClone.CreatedUtc
            },
            cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VoiceClone>> ListAsync(string aUserId, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            SELECT "CloneId", "UserId", "VoiceName", "Language", "IsRealPerson", "SubjectReference", "Status", "CreatedUtc"
            FROM "VoiceClone"
            WHERE "UserId" = @aUserId
            ORDER BY "CreatedUtc" DESC
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vRows = await vConnection.QueryAsync<VoiceClone>(new CommandDefinition(
            Sql, new { aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        return vRows.ToList();
    }
}
