namespace TrStudio.Models.Contracts;

/// <summary>
/// The audio→avatar alignment handoff contract (FN-035/BRD-26). This is a STRUCTURAL handoff: the
/// voice job output plus timing metadata form the avatar-pipeline job input; the avatar pipeline
/// itself is built in Phase 5.
/// </summary>
public class AvatarHandoffResponse
{
    /// <summary>The source voice/audio asset id.</summary>
    public Guid AssetId { get; set; }

    /// <summary>The target downstream pipeline (always <c>avatar</c> for this handoff).</summary>
    public string TargetPipeline { get; set; } = "avatar";

    /// <summary>Handoff status: <c>ready-for-avatar</c> when the audio is present, else <c>pending</c>.</summary>
    public string Status { get; set; } = "pending";

    /// <summary>Storage path of the audio the avatar pipeline aligns against.</summary>
    public string AudioStoragePath { get; set; } = string.Empty;

    /// <summary>Duration of the audio in seconds.</summary>
    public double DurationSeconds { get; set; }

    /// <summary>Audio sample rate (Hz) for the alignment stage.</summary>
    public int SampleRateHz { get; set; }

    /// <summary>Language of the audio.</summary>
    public string Language { get; set; } = "en";

    /// <summary>The serialized avatar-pipeline job input contract (consumed by Phase-5 RAG/avatar work).</summary>
    public string AvatarJobInputJson { get; set; } = "{}";

    /// <summary>A human-readable description of the handoff outcome.</summary>
    public string Message { get; set; } = string.Empty;
}
