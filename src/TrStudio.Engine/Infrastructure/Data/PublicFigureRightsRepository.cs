using Dapper;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IPublicFigureRightsRepository"/> over the <c>"PublicFigureRights"</c>
/// table (script 0900, SAFE-05). Reads are owner-filtered by the AppManager user id (BRD-81);
/// identifiers are quoted PascalCase. Absence of a matching row blocks a public-figure depiction by
/// default (fail closed).
/// </summary>
public class PublicFigureRightsRepository : IPublicFigureRightsRepository
{
    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public PublicFigureRightsRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(PublicFigureRights aRights, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            INSERT INTO "PublicFigureRights"
                ("RightsId", "UserId", "SubjectReference", "RightsType", "RightsDetail", "CreatedUtc")
            VALUES (@aRightsId, @aUserId, @aSubjectReference, @aRightsType, @aRightsDetail, @aCreatedUtc)
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(Sql, new
        {
            aRightsId = aRights.RightsId,
            aUserId = aRights.UserId,
            aSubjectReference = aRights.SubjectReference,
            aRightsType = aRights.RightsType,
            aRightsDetail = aRights.RightsDetail,
            aCreatedUtc = aRights.CreatedUtc
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> HasRightsAsync(string aUserId, string aSubjectReference, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            SELECT EXISTS (
                SELECT 1 FROM "PublicFigureRights"
                WHERE "UserId" = @aUserId AND "SubjectReference" = @aSubjectReference
            )
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.ExecuteScalarAsync<bool>(new CommandDefinition(
            Sql,
            new { aUserId, aSubjectReference },
            cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }
}
