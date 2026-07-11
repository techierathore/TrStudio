namespace TrStudio.Models.Enums;

/// <summary>
/// The realism style of a synthetic character persona (FN-036/BRD-27). The flag maps to a
/// <see cref="SafetyClass"/> and feeds the safety pipeline's NSFW-scope check (FN-063): a
/// photoreal persona is held to real-person-gated scrutiny, while a stylized persona is treated
/// as synthetic-default. In every mode, photoreal-of-real-person and minor content stay blocked.
/// </summary>
public enum CharacterRealism
{
    /// <summary>A photorealistic persona; held to real-person-gated safety scrutiny.</summary>
    Photoreal,

    /// <summary>A stylized/illustrated persona; treated as fully-synthetic (synthetic-default).</summary>
    Stylized
}
