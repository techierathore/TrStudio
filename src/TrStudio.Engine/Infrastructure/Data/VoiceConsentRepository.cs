using Dapper;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IVoiceConsentRepository"/> over the <c>"VoiceConsentEvidence"</c> table
/// (script 0800, FN-032 / SAFE-03). Reads are owner-filtered by the AppManager user id (BRD-81); the
/// parameterised SQL quotes all PascalCase identifiers.
/// </summary>
public class VoiceConsentRepository : IVoiceConsentRepository
{
    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>
    /// Initialises the repository.
    /// </summary>
    /// <param name="aConnectionFactory">Factory producing open PostgreSQL connections.</param>
    public VoiceConsentRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(VoiceConsentEvidence aEvidence, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            INSERT INTO "VoiceConsentEvidence"
                ("EvidenceId", "UserId", "SubjectReference", "EvidenceType", "EvidenceDetail", "ConsentGranted", "CreatedUtc")
            VALUES (@aEvidenceId, @aUserId, @aSubjectReference, @aEvidenceType, @aEvidenceDetail, @aConsentGranted, @aCreatedUtc)
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(
            Sql,
            new
            {
                aEvidenceId = aEvidence.EvidenceId,
                aUserId = aEvidence.UserId,
                aSubjectReference = aEvidence.SubjectReference,
                aEvidenceType = aEvidence.EvidenceType,
                aEvidenceDetail = aEvidence.EvidenceDetail,
                aConsentGranted = aEvidence.ConsentGranted,
                aCreatedUtc = aEvidence.CreatedUtc
            },
            cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> HasGrantedConsentAsync(string aUserId, string aSubjectReference, CancellationToken aCancellationToken = default)
    {
        const string Sql = """
            SELECT EXISTS (
                SELECT 1 FROM "VoiceConsentEvidence"
                WHERE "UserId" = @aUserId AND "SubjectReference" = @aSubjectReference AND "ConsentGranted" = TRUE
            )
            """;

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        return await vConnection.ExecuteScalarAsync<bool>(new CommandDefinition(
            Sql,
            new { aUserId, aSubjectReference },
            cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }
}
