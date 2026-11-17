using Dapper;
using TrStudio.Engine.Domain.Sequence;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IClipSequenceRepository"/> over <c>"ClipSequence"</c> and
/// <c>"ClipSequenceShot"</c> (script 1720). Every read and update carries the owner's id (BRD-81).
/// </summary>
public class ClipSequenceRepository : IClipSequenceRepository
{
    private const string SequenceColumns =
        "\"ClipSequenceId\" AS \"SequenceId\", \"UserId\", \"SubjectKind\", \"SubjectId\", \"State\", \"Message\", " +
        "\"Duration\", \"Resolution\", \"JoinedAssetId\", \"CreatedUtc\", \"FinishedUtc\"";

    private const string ShotColumns =
        "\"ClipSequenceShotId\" AS \"ShotId\", \"ClipSequenceId\" AS \"SequenceId\", \"UserId\", \"ShotIndex\", \"Prompt\", " +
        "\"JobId\", \"State\", \"StartFrameAssetId\", \"ResultAssetId\"";

    private readonly IDbConnectionFactory objConnectionFactory;

    /// <summary>Initialises the repository.</summary>
    /// <param name="aConnectionFactory">Factory producing open connections.</param>
    public ClipSequenceRepository(IDbConnectionFactory aConnectionFactory)
    {
        objConnectionFactory = aConnectionFactory;
    }

    /// <inheritdoc />
    public async Task InsertAsync(ClipSequence aSequence, CancellationToken aCancellationToken = default)
    {
        const string vSequenceSql =
            "INSERT INTO \"ClipSequence\" (\"ClipSequenceId\", \"UserId\", \"SubjectKind\", \"SubjectId\", \"State\", \"Message\", " +
            "\"Duration\", \"Resolution\", \"JoinedAssetId\", \"CreatedUtc\", \"FinishedUtc\") " +
            "VALUES (@SequenceId, @UserId, @SubjectKind, @SubjectId, @State, @Message, @Duration, @Resolution, @JoinedAssetId, @CreatedUtc, @FinishedUtc)";
        const string vShotSql =
            "INSERT INTO \"ClipSequenceShot\" (\"ClipSequenceShotId\", \"ClipSequenceId\", \"UserId\", \"ShotIndex\", \"Prompt\", " +
            "\"JobId\", \"State\", \"StartFrameAssetId\", \"ResultAssetId\") " +
            "VALUES (@ShotId, @SequenceId, @UserId, @ShotIndex, @Prompt, @JobId, @State, @StartFrameAssetId, @ResultAssetId)";

        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        using var vTransaction = vConnection.BeginTransaction();
        await vConnection.ExecuteAsync(new CommandDefinition(vSequenceSql, aSequence, vTransaction, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        foreach (var vShot in aSequence.Shots)
        {
            await vConnection.ExecuteAsync(new CommandDefinition(vShotSql, vShot, vTransaction, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        }

        vTransaction.Commit();
    }

    /// <inheritdoc />
    public async Task<ClipSequence?> GetAsync(Guid aSequenceId, string aUserId, CancellationToken aCancellationToken = default)
    {
        var vSql = $"SELECT {SequenceColumns} FROM \"ClipSequence\" WHERE \"ClipSequenceId\" = @aSequenceId AND \"UserId\" = @aUserId";
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vSequence = await vConnection.QuerySingleOrDefaultAsync<ClipSequence>(new CommandDefinition(vSql, new { aSequenceId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
        if (vSequence is not null)
        {
            vSequence.Shots = await LoadShotsAsync(vConnection, new[] { aSequenceId }, aUserId, aCancellationToken).ConfigureAwait(false);
        }

        return vSequence;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClipSequence>> ListAsync(string aUserId, CancellationToken aCancellationToken = default)
    {
        var vSql = $"SELECT {SequenceColumns} FROM \"ClipSequence\" WHERE \"UserId\" = @aUserId ORDER BY \"CreatedUtc\" DESC LIMIT 50";
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        var vSequences = (await vConnection.QueryAsync<ClipSequence>(new CommandDefinition(vSql, new { aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false)).ToList();
        foreach (var vSequence in vSequences)
        {
            vSequence.Shots = await LoadShotsAsync(vConnection, new[] { vSequence.SequenceId }, aUserId, aCancellationToken).ConfigureAwait(false);
        }

        return vSequences;
    }

    /// <inheritdoc />
    public async Task UpdateSequenceAsync(ClipSequence aSequence, CancellationToken aCancellationToken = default)
    {
        const string vSql =
            "UPDATE \"ClipSequence\" SET \"State\" = @State, \"Message\" = @Message, \"JoinedAssetId\" = @JoinedAssetId, \"FinishedUtc\" = @FinishedUtc " +
            "WHERE \"ClipSequenceId\" = @SequenceId AND \"UserId\" = @UserId";
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(vSql, aSequence, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateShotAsync(ClipSequenceShot aShot, CancellationToken aCancellationToken = default)
    {
        const string vSql =
            "UPDATE \"ClipSequenceShot\" SET \"JobId\" = @JobId, \"State\" = @State, \"StartFrameAssetId\" = @StartFrameAssetId, \"ResultAssetId\" = @ResultAssetId " +
            "WHERE \"ClipSequenceShotId\" = @ShotId AND \"UserId\" = @UserId";
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(vSql, aShot, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task FailUnfinishedAsync(string aReason, CancellationToken aCancellationToken = default)
    {
        const string vSql =
            "UPDATE \"ClipSequence\" SET \"State\" = 'Failed', \"Message\" = @aReason, \"FinishedUtc\" = now() " +
            "WHERE \"State\" IN ('Running', 'Joining')";
        using var vConnection = await objConnectionFactory.CreateOpenConnectionAsync(aCancellationToken).ConfigureAwait(false);
        await vConnection.ExecuteAsync(new CommandDefinition(vSql, new { aReason }, cancellationToken: aCancellationToken)).ConfigureAwait(false);
    }

    private static async Task<List<ClipSequenceShot>> LoadShotsAsync(System.Data.IDbConnection aConnection, IEnumerable<Guid> aSequenceIds, string aUserId, CancellationToken aCancellationToken)
    {
        var vShots = new List<ClipSequenceShot>();
        foreach (var vId in aSequenceIds)
        {
            var vSql = $"SELECT {ShotColumns} FROM \"ClipSequenceShot\" WHERE \"ClipSequenceId\" = @vId AND \"UserId\" = @aUserId ORDER BY \"ShotIndex\"";
            vShots.AddRange(await aConnection.QueryAsync<ClipSequenceShot>(new CommandDefinition(vSql, new { vId, aUserId }, cancellationToken: aCancellationToken)).ConfigureAwait(false));
        }

        return vShots;
    }
}
