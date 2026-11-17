namespace TrStudio.Models.Contracts;

/// <summary>Creates a digital twin (REQ-FN-041).</summary>
public class DigitalTwinCreateRequest
{
    /// <summary>The twin's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The person shown; consent must be recorded for this subject.</summary>
    public string SubjectReference { get; set; } = string.Empty;

    /// <summary>One to ten uploaded reference pictures.</summary>
    public List<Guid> PictureAssetIds { get; set; } = new();

    /// <summary>The uploaded reference clip (MP4 or MOV, 1 to 15 seconds).</summary>
    public Guid ClipAssetId { get; set; }

    /// <summary>The reference clip length in seconds (1 to 15).</summary>
    public double ClipSeconds { get; set; }

    /// <summary>The uploaded voice sample (WAV or MP3, 5 to 15 seconds).</summary>
    public Guid VoiceSampleAssetId { get; set; }

    /// <summary>The voice sample length in seconds, when the screen knows it (5 to 15).</summary>
    public double? VoiceSampleSeconds { get; set; }
}

/// <summary>A digital twin as the screens see it.</summary>
public class DigitalTwinResponse
{
    /// <summary>The twin id.</summary>
    public Guid DigitalTwinId { get; set; }

    /// <summary>The display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The person shown.</summary>
    public string SubjectReference { get; set; } = string.Empty;

    /// <summary>The reference picture asset ids.</summary>
    public List<Guid> PictureAssetIds { get; set; } = new();

    /// <summary>The reference clip asset id.</summary>
    public Guid ClipAssetId { get; set; }

    /// <summary>The reference clip length in seconds.</summary>
    public double ClipSeconds { get; set; }

    /// <summary>The voice sample asset id.</summary>
    public Guid VoiceSampleAssetId { get; set; }

    /// <summary>The Venice voice id.</summary>
    public string VeniceVoiceId { get; set; } = string.Empty;

    /// <summary>When the Venice voice was made.</summary>
    public DateTime VoiceCreatedUtc { get; set; }

    /// <summary>When the twin was created.</summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>Asks for one clip of a twin saying a line.</summary>
public class DigitalTwinClipRequest
{
    /// <summary>The words the person says.</summary>
    public string Line { get; set; } = string.Empty;

    /// <summary>Venice duration (e.g. "2s"); null for the row's default.</summary>
    public string? Duration { get; set; }

    /// <summary>Venice resolution (e.g. "480p"); null for 480p.</summary>
    public string? Resolution { get; set; }
}

/// <summary>The job a twin clip was queued as.</summary>
public class DigitalTwinClipResponse
{
    /// <summary>The generation job id.</summary>
    public Guid JobId { get; set; }
}

/// <summary>The owner's digital twins on/off setting.</summary>
public class DigitalTwinPolicyResponse
{
    /// <summary>Whether twins are turned on (off on a fresh app).</summary>
    public bool Enabled { get; set; }
}

/// <summary>Sets the owner's digital twins on/off setting.</summary>
public class DigitalTwinPolicyUpdateRequest
{
    /// <summary>Whether twins should be on.</summary>
    public bool Enabled { get; set; }
}

/// <summary>Uploads one twin file (picture, clip or voice sample).</summary>
public class DigitalTwinUploadRequest
{
    /// <summary>The file name; its extension names the format.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary><c>picture</c>, <c>clip</c> or <c>voice</c>.</summary>
    public string Kind { get; set; } = "picture";

    /// <summary>The file bytes, base64.</summary>
    public string Base64Content { get; set; } = string.Empty;
}

/// <summary>The stored file's asset id.</summary>
public class DigitalTwinUploadResponse
{
    /// <summary>The asset id to use when creating the twin.</summary>
    public Guid AssetId { get; set; }
}
