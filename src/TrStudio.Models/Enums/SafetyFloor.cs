namespace TrStudio.Models.Enums;

/// <summary>
/// The non-disableable safety floors (SAFE-01..06 / BRD-60..65) plus the two mode-scoped gates
/// that sit on top of them. A blocked generation records which floor rejected it in the audit
/// trail (SAFE-06). The floors themselves are never toggleable — including under the owner-only
/// Unrestricted (NSFW) mode (F-NSFW).
/// </summary>
public enum SafetyFloor
{
    /// <summary>No floor rejected the generation (an allow decision).</summary>
    None = 0,

    /// <summary>SAFE-01 (BRD-60): CSAM / minor-sexual content — hash match + classifier, hard block.</summary>
    Csam = 1,

    /// <summary>SAFE-02 (BRD-61): sexualisation/face-swap of a real identifiable person; NCII / sexual deepfakes.</summary>
    RealPersonSexual = 2,

    /// <summary>SAFE-03 (BRD-62): recorded-consent required for any real-person face or voice clone.</summary>
    Consent = 3,

    /// <summary>SAFE-04 (BRD-63): provenance / AI-disclosure metadata missing on an output.</summary>
    Provenance = 4,

    /// <summary>SAFE-05 (BRD-64): impersonation of a real public figure without a recorded rights record.</summary>
    PublicFigure = 5,

    /// <summary>SAFE-06 (BRD-65): audit-trail / policy floor.</summary>
    Audit = 6,

    /// <summary>F-NSFW scope (BRD-80): NSFW mode is limited to fully-synthetic adult (18+) personas.</summary>
    NsfwScope = 7,

    /// <summary>The toggleable general SFW filter (F-NSFW / BRD-79) — the ONLY gate the owner may lift.</summary>
    GeneralSfwFilter = 8
}
