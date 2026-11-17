namespace TrStudio.Engine.Domain.Sequence;

/// <summary>Whose shots a sequence is made of.</summary>
public static class SequenceSubject
{
    /// <summary>An avatar character from the Character library.</summary>
    public const string Character = "character";

    /// <summary>A digital twin.</summary>
    public const string Twin = "twin";
}

/// <summary>The states of a sequence and of a shot.</summary>
public static class SequenceStates
{
    /// <summary>Shot waiting for its turn.</summary>
    public const string Pending = "Pending";

    /// <summary>Sequence or shot being made.</summary>
    public const string Running = "Running";

    /// <summary>Sequence whose shots are made and are being joined.</summary>
    public const string Joining = "Joining";

    /// <summary>Done.</summary>
    public const string Succeeded = "Succeeded";

    /// <summary>Stopped with a reason.</summary>
    public const string Failed = "Failed";
}

/// <summary>What a creator asks for: 2 to 6 shots of one subject.</summary>
public sealed class SequenceRequest
{
    /// <summary>The fewest shots a sequence has.</summary>
    public const int MinShots = 2;

    /// <summary>The most shots a sequence has.</summary>
    public const int MaxShots = 6;

    /// <summary>The character, when the subject is a character.</summary>
    public Guid? CharacterId { get; set; }

    /// <summary>The digital twin, when the subject is a twin.</summary>
    public Guid? TwinId { get; set; }

    /// <summary>The shots, in order.</summary>
    public List<SequenceShotRequest> Shots { get; set; } = new();

    /// <summary>Venice clip length for each shot (e.g. "2s"); empty for the cheapest.</summary>
    public string? Duration { get; set; }

    /// <summary>Venice resolution for each shot (e.g. "480p"); empty for the cheapest.</summary>
    public string? Resolution { get; set; }
}

/// <summary>One shot as asked for.</summary>
public sealed class SequenceShotRequest
{
    /// <summary>The one line describing the shot (for a twin, the words said).</summary>
    public string Prompt { get; set; } = string.Empty;
}

/// <summary>Venice's price for a sequence.</summary>
/// <param name="TotalUsd">The sum of every shot's quote.</param>
/// <param name="PerShot">Each shot's quote, in order.</param>
public sealed record SequenceQuote(decimal TotalUsd, IReadOnlyList<decimal> PerShot);

/// <summary>A stored sequence.</summary>
public sealed class ClipSequence
{
    /// <summary>The sequence id.</summary>
    public Guid SequenceId { get; set; }

    /// <summary>The owner.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary><see cref="SequenceSubject.Character"/> or <see cref="SequenceSubject.Twin"/>.</summary>
    public string SubjectKind { get; set; } = SequenceSubject.Character;

    /// <summary>The character or twin.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>One of <see cref="SequenceStates"/>.</summary>
    public string State { get; set; } = SequenceStates.Running;

    /// <summary>Plain-words detail, e.g. why it failed.</summary>
    public string? Message { get; set; }

    /// <summary>Clip length of each shot.</summary>
    public string? Duration { get; set; }

    /// <summary>Resolution of each shot.</summary>
    public string? Resolution { get; set; }

    /// <summary>The joined video's asset, once done.</summary>
    public Guid? JoinedAssetId { get; set; }

    /// <summary>When it was created (UTC).</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>When it ended (UTC).</summary>
    public DateTime? FinishedUtc { get; set; }

    /// <summary>The shots, in order.</summary>
    public List<ClipSequenceShot> Shots { get; set; } = new();
}

/// <summary>One stored shot.</summary>
public sealed class ClipSequenceShot
{
    /// <summary>The shot row id.</summary>
    public Guid ShotId { get; set; }

    /// <summary>The sequence it belongs to.</summary>
    public Guid SequenceId { get; set; }

    /// <summary>The owner.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Position, starting at 1.</summary>
    public int ShotIndex { get; set; }

    /// <summary>The line of the shot.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>The generation job that made it.</summary>
    public Guid? JobId { get; set; }

    /// <summary>One of <see cref="SequenceStates"/>.</summary>
    public string State { get; set; } = SequenceStates.Pending;

    /// <summary>The picture the shot opened on (the last frame of the shot before).</summary>
    public Guid? StartFrameAssetId { get; set; }

    /// <summary>The shot's video asset.</summary>
    public Guid? ResultAssetId { get; set; }
}

/// <summary>Stores sequences and their shots, owner-scoped.</summary>
public interface IClipSequenceRepository
{
    /// <summary>Inserts a sequence with its shots.</summary>
    /// <param name="aSequence">The sequence.</param>
    /// <param name="aCancellationToken">Token to cancel.</param>
    /// <returns>A task.</returns>
    Task InsertAsync(ClipSequence aSequence, CancellationToken aCancellationToken = default);

    /// <summary>Gets one sequence with its shots.</summary>
    /// <param name="aSequenceId">The sequence.</param>
    /// <param name="aUserId">The owner.</param>
    /// <param name="aCancellationToken">Token to cancel.</param>
    /// <returns>The sequence, or null when it is not this user's.</returns>
    Task<ClipSequence?> GetAsync(Guid aSequenceId, string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>Lists the user's sequences, newest first, with their shots.</summary>
    /// <param name="aUserId">The owner.</param>
    /// <param name="aCancellationToken">Token to cancel.</param>
    /// <returns>The sequences.</returns>
    Task<IReadOnlyList<ClipSequence>> ListAsync(string aUserId, CancellationToken aCancellationToken = default);

    /// <summary>Updates a sequence's state, message, joined asset and end time.</summary>
    /// <param name="aSequence">The sequence.</param>
    /// <param name="aCancellationToken">Token to cancel.</param>
    /// <returns>A task.</returns>
    Task UpdateSequenceAsync(ClipSequence aSequence, CancellationToken aCancellationToken = default);

    /// <summary>Updates a shot's job, state, start frame and result.</summary>
    /// <param name="aShot">The shot.</param>
    /// <param name="aCancellationToken">Token to cancel.</param>
    /// <returns>A task.</returns>
    Task UpdateShotAsync(ClipSequenceShot aShot, CancellationToken aCancellationToken = default);

    /// <summary>Marks every sequence still Running or Joining as Failed (the app closed while it ran).</summary>
    /// <param name="aReason">The reason shown.</param>
    /// <param name="aCancellationToken">Token to cancel.</param>
    /// <returns>A task.</returns>
    Task FailUnfinishedAsync(string aReason, CancellationToken aCancellationToken = default);
}
