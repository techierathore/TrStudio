namespace TrStudio.Models.Contracts;

/// <summary>
/// Request to build the audio→avatar alignment handoff (FN-035/BRD-26). Takes a completed voice
/// asset and produces the structural contract the Phase-5 avatar pipeline consumes as its input.
/// </summary>
public class AvatarHandoffRequest
{
    /// <summary>The produced voice/audio asset id.</summary>
    public Guid AssetId { get; set; }

    /// <summary>Storage path of the audio binary the avatar pipeline will align against.</summary>
    public string AudioStoragePath { get; set; } = string.Empty;

    /// <summary>Duration of the audio in seconds (timing metadata).</summary>
    public double DurationSeconds { get; set; }

    /// <summary>Language of the audio (drives phoneme/viseme alignment downstream).</summary>
    public string Language { get; set; } = "en";
}
