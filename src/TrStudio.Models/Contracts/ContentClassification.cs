namespace TrStudio.Models.Contracts;

/// <summary>
/// The result of a content-safety classification (SAFE-01/02). The real model (CSAM/nudity
/// classifier) is pluggable; when the authoritative source is unavailable the pipeline treats
/// risk-elevated content as blocked (fail closed).
/// </summary>
public class ContentClassification
{
    /// <summary>Whether the authoritative classifier model is installed and produced this result.</summary>
    public bool SourceAvailable { get; init; }

    /// <summary>The content is (or is very likely) CSAM.</summary>
    public bool Csam { get; init; }

    /// <summary>The content sexualises a minor.</summary>
    public bool MinorSexual { get; init; }

    /// <summary>The content sexualises a real, identifiable person (NCII / sexual deepfake signal).</summary>
    public bool RealPersonSexual { get; init; }

    /// <summary>The content is adult/sexual in nature (drives the general SFW filter, not a floor by itself).</summary>
    public bool AdultSexual { get; init; }

    /// <summary>A short, non-revealing detail for the audit trail.</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>Whether any prohibited-floor category (CSAM / minor / real-person sexual) fired.</summary>
    public bool AnyProhibited => Csam || MinorSexual || RealPersonSexual;
}
