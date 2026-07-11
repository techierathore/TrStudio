using TrStudio.Models.Enums;

namespace TrStudio.Models.Entities;

/// <summary>
/// A first-class synthetic-character persona (FN-036/BRD-27). A character carries a LOCKED
/// IDENTITY — a reference-asset set plus a deterministic seed/embedding and an optional LoRA
/// reference — so the same persona is reproduced across studios (avatar, image, video). The
/// <see cref="Realism"/> flag maps to <see cref="SafetyClass"/> and, together with the real-person /
/// minor flags, feeds the non-disableable safety floors (FN-063). Owner-scoped by
/// <see cref="UserId"/> (BRD-81).
/// </summary>
public class Character
{
    /// <summary>Unique character identifier.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>The AppManager user id owning this character (per-user isolation, BRD-81).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Human-readable persona name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional persona description / short bio.</summary>
    public string? Description { get; set; }

    /// <summary>Realism style; maps to <see cref="SafetyClass"/> and scopes the safety floors (FN-063).</summary>
    public CharacterRealism Realism { get; set; }

    /// <summary>The safety classification derived from <see cref="Realism"/> and the real-person flag.</summary>
    public SafetyClass SafetyClass { get; set; }

    /// <summary>The locked identity seed reused across every studio for a reproducible persona.</summary>
    public long Seed { get; set; }

    /// <summary>Optional locked identity embedding reference (face/style embedding id or vector ref).</summary>
    public string? IdentityEmbedding { get; set; }

    /// <summary>Optional LoRA weight reference pinning the persona's likeness.</summary>
    public string? LoraRef { get; set; }

    /// <summary>JSON array of locked reference-asset ids/paths that define the persona's likeness.</summary>
    public string ReferenceAssetsJson { get; set; } = "[]";

    /// <summary>Whether the persona depicts a real, identifiable person (SAFE-02/03 gating).</summary>
    public bool IsRealPerson { get; set; }

    /// <summary>Whether the persona is a photoreal likeness of a real person (blocked, SAFE-02/BRD-80).</summary>
    public bool PhotorealOfRealPerson { get; set; }

    /// <summary>Whether the persona depicts a minor (never permitted in a sexual context, SAFE-01).</summary>
    public bool DepictsMinor { get; set; }

    /// <summary>Whether the persona is an adult (18+); required for the owner-only NSFW scope (BRD-80).</summary>
    public bool IsAdult { get; set; } = true;

    /// <summary>UTC timestamp when the character was created.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>UTC timestamp when the character was last updated.</summary>
    public DateTime UpdatedUtc { get; set; }
}
