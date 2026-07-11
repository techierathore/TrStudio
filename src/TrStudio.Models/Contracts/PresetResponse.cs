namespace TrStudio.Models.Contracts;

/// <summary>
/// A creator-library preset as returned by the presets endpoints (REQ-FN-066/BRD-50).
/// </summary>
public class PresetResponse
{
    /// <summary>Unique preset identifier.</summary>
    public Guid PresetId { get; set; }

    /// <summary>The preset type: <c>prompt</c>, <c>script</c>, <c>style-preset</c> or <c>voice-preset</c>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The creator-facing preset name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional free-text notes describing the preset.</summary>
    public string? Description { get; set; }

    /// <summary>The preset body (prompt text, script text or a settings JSON blob).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>How many times this preset has been applied (reuse metric).</summary>
    public int UseCount { get; set; }

    /// <summary>UTC timestamp when the preset was created.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>UTC timestamp when the preset was last updated.</summary>
    public DateTime UpdatedUtc { get; set; }
}
