using Dapper;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IConsentRepository"/> over the <c>"ConsentEvidence"</c> table (script
/// 0900, SAFE-03). Covers both face and voice modalities. Reads are owner-filtered by the AppManager
/// user id (BRD-81); the parameterised SQL quotes all PascalCase identifiers.
/// </summary>
public class ConsentRepository : IConsentRepository
{
    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public ConsentRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(ConsentEvidence aEvidence, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            INSERT INTO "ConsentEvidence"
                ("EvidenceId", "UserId", "SubjectReference", "Modality", "EvidenceType", "EvidenceDetail", "ConsentGranted", "CreatedUtc")
            VALUES (@aEvidenceId, @aUserId, @aSubjectReference, @aModality, @aEvidenceType, @aEvidenceDetail, @aConsentGranted, @aCreatedUtc)
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(Sql, new
        {
            aEvidenceId = aEvidence.EvidenceId,
            aUserId = aEvidence.UserId,
            aSubjectReference = aEvidence.SubjectReference,
            aModality = aEvidence.Modality.ToString(),
            aEvidenceType = aEvidence.EvidenceType,
            aEvidenceDetail = aEvidence.EvidenceDetail,
            aConsentGranted = aEvidence.ConsentGranted,
            aCreatedUtc = aEvidence.CreatedUtc
        }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> HasGrantedConsentAsync(string aUserId, string aSubjectReference, ConsentModality aModality, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            SELECT EXISTS (
                SELECT 1 FROM "ConsentEvidence"
                WHERE "UserId" = @aUserId AND "SubjectReference" = @aSubjectReference
                  AND "Modality" = @aModality AND "ConsentGranted" = TRUE
            )
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.ExecuteScalarAsync<bool>(new CommandDefinition(
            Sql,
            new { aUserId, aSubjectReference, aModality = aModality.ToString() },
            cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }
}
