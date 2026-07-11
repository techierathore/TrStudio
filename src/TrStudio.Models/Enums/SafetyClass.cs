namespace TrStudio.Models.Enums;

/// <summary>
/// The safety classification of a registry model, scoping which safety floors apply
/// to that model's jobs (Architecture §6/§9, SAFE-01..06).
/// </summary>
public enum SafetyClass
{
    /// <summary>Fully-synthetic content; the default safety pipeline applies.</summary>
    SyntheticDefault,

    /// <summary>Involves real-person likeness; consent gating and deepfake refusal are enforced.</summary>
    RealPersonGated
}
