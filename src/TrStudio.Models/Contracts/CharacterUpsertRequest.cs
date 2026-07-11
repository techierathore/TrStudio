using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// Request to create or update a synthetic-character persona (FN-036/BRD-27). On create the service
/// mints the locked identity (seed + reference set); on update the locked identity is preserved and
/// only mutable descriptive fields change. The <see cref="Realism"/> flag scopes the safety floors.
/// </summary>
public class CharacterUpsertRequest
{
    /// <summary>The persona name (required).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional persona description / short bio.</summary>
    public string? Description { get; set; }

    /// <summary>Realism style (<c>Photoreal</c> or <c>Stylized</c>); maps to the safety class (FN-063).</summary>
    public CharacterRealism Realism { get; set; } = CharacterRealism.Stylized;

    /// <summary>Optional fixed identity seed; when null the service mints one to lock the persona.</summary>
    public long? Seed { get; set; }

    /// <summary>Optional locked identity embedding reference.</summary>
    public string? IdentityEmbedding { get; set; }

    /// <summary>Optional LoRA weight reference pinning the persona likeness.</summary>
    public string? LoraRef { get; set; }

    /// <summary>Reference-asset ids/paths that define the persona likeness (locked on create).</summary>
    public List<string> ReferenceAssets { get; set; } = new();

    /// <summary>Whether the persona depicts a real, identifiable person (SAFE-02/03 gating).</summary>
    public bool IsRealPerson { get; set; }

    /// <summary>Whether the persona is a photoreal likeness of a real person (blocked, SAFE-02/BRD-80).</summary>
    public bool PhotorealOfRealPerson { get; set; }

    /// <summary>Whether the persona depicts a minor (SAFE-01).</summary>
    public bool DepictsMinor { get; set; }

    /// <summary>Whether the persona is an adult (18+); required for the owner-only NSFW scope (BRD-80).</summary>
    public bool IsAdult { get; set; } = true;
}
