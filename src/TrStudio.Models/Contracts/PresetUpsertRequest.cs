namespace TrStudio.Models.Contracts;

/// <summary>
/// Create/update body for a creator-library preset (REQ-FN-066/BRD-50).
/// </summary>
public class PresetUpsertRequest
{
    /// <summary>The preset type: <c>prompt</c>, <c>script</c>, <c>style-preset</c> or <c>voice-preset</c>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The creator-facing preset name (required).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional free-text notes describing the preset.</summary>
    public string? Description { get; set; }

    /// <summary>The preset body (prompt text, script text or a settings JSON blob).</summary>
    public string Content { get; set; } = string.Empty;
}
