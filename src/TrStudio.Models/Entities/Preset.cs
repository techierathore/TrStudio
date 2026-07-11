namespace TrStudio.Models.Entities;

/// <summary>
/// A reusable creator library item (REQ-FN-066/BRD-50): a saved prompt, script, style-preset or
/// voice-preset. Owner-scoped by <see cref="UserId"/> (the AppManager user id); every repository
/// query applies the owner filter server-side (BRD-81). The <see cref="UseCount"/> increments each
/// time the preset is applied so the library can surface a creator's most-reused items.
/// </summary>
public class Preset
{
    /// <summary>Unique preset identifier.</summary>
    public Guid PresetId { get; set; }

    /// <summary>The AppManager user id owning this preset (per-user isolation, BRD-81).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The preset type: <c>prompt</c>, <c>script</c>, <c>style-preset</c> or <c>voice-preset</c>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The creator-facing preset name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional free-text notes describing the preset.</summary>
    public string? Description { get; set; }

    /// <summary>The preset body (prompt text, script text or a preset settings JSON blob).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>How many times this preset has been applied (reuse metric).</summary>
    public int UseCount { get; set; }

    /// <summary>UTC timestamp when the preset was created.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>UTC timestamp when the preset was last updated (name/content edit or a use bump).</summary>
    public DateTime UpdatedUtc { get; set; }
}
