namespace TrStudio.Models.Entities;

/// <summary>
/// A creator's digital twin of a real person (REQ-FN-041): reference pictures, one reference clip,
/// one voice sample and the Venice voice made from it. Owner-scoped by <see cref="UserId"/> (BRD-81).
/// </summary>
public class DigitalTwin
{
    /// <summary>The twin id.</summary>
    public Guid DigitalTwinId { get; set; }

    /// <summary>The owning user.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The twin's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The person shown (the subject consent is recorded against).</summary>
    public string SubjectReference { get; set; } = string.Empty;

    /// <summary>The reference picture asset ids, as a JSON array of ids.</summary>
    public string PictureAssetIdsJson { get; set; } = "[]";

    /// <summary>The reference clip asset id.</summary>
    public Guid ClipAssetId { get; set; }

    /// <summary>The reference clip length in seconds (1 to 15).</summary>
    public double ClipSeconds { get; set; }

    /// <summary>The voice sample asset id (5 to 15 seconds).</summary>
    public Guid VoiceSampleAssetId { get; set; }

    /// <summary>The Venice voice id (vv_…); it expires after 7 days.</summary>
    public string VeniceVoiceId { get; set; } = string.Empty;

    /// <summary>When the Venice voice was made.</summary>
    public DateTime VoiceCreatedUtc { get; set; }

    /// <summary>When the twin was created.</summary>
    public DateTime CreatedUtc { get; set; }
}
