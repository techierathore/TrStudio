namespace TrStudio.Models.Enums;

/// <summary>
/// The modality a recorded-consent record covers (SAFE-03 / BRD-62). The shared consent service
/// gates both real-person face and real-person voice work on retained evidence.
/// </summary>
public enum ConsentModality
{
    /// <summary>Consent to clone / depict a real person's face or likeness.</summary>
    Face,

    /// <summary>Consent to clone a real person's voice.</summary>
    Voice
}
