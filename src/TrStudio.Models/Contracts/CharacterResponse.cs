using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// The creator-facing view of a synthetic character (FN-036). Carries the locked identity
/// (seed/embedding/references) so a saved character reuses the same persona across studios, plus the
/// derived safety class that scopes the safety floors.
/// </summary>
public class CharacterResponse
{
    /// <summary>Unique character identifier.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>The persona name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional persona description.</summary>
    public string? Description { get; set; }

    /// <summary>Realism style.</summary>
    public CharacterRealism Realism { get; set; }

    /// <summary>The safety classification derived from the realism/real-person flags (FN-063).</summary>
    public SafetyClass SafetyClass { get; set; }

    /// <summary>The locked identity seed reused across studios.</summary>
    public long Seed { get; set; }

    /// <summary>The locked identity embedding reference, if any.</summary>
    public string? IdentityEmbedding { get; set; }

    /// <summary>The LoRA reference pinning the persona likeness, if any.</summary>
    public string? LoraRef { get; set; }

    /// <summary>The locked reference-asset ids/paths defining the persona likeness.</summary>
    public List<string> ReferenceAssets { get; set; } = new();

    /// <summary>Whether the persona depicts a real, identifiable person.</summary>
    public bool IsRealPerson { get; set; }

    /// <summary>Whether the persona is a photoreal likeness of a real person.</summary>
    public bool PhotorealOfRealPerson { get; set; }

    /// <summary>Whether the persona depicts a minor.</summary>
    public bool DepictsMinor { get; set; }

    /// <summary>Whether the persona is an adult (18+).</summary>
    public bool IsAdult { get; set; }

    /// <summary>UTC timestamp when the character was created.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>UTC timestamp when the character was last updated.</summary>
    public DateTime UpdatedUtc { get; set; }
}
