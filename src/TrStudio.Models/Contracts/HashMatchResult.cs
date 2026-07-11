namespace TrStudio.Models.Contracts;

/// <summary>
/// The result of matching content against a known-bad perceptual-hash database (SAFE-01, CSAM
/// prevention by design). The real hash source is pluggable; an unavailable source never silently
/// allows risk-elevated content (fail closed).
/// </summary>
public class HashMatchResult
{
    /// <summary>Whether the hash database is installed and produced this result.</summary>
    public bool SourceAvailable { get; init; }

    /// <summary>Whether the content matched a known-bad hash.</summary>
    public bool IsMatch { get; init; }

    /// <summary>A short, non-revealing detail for the audit trail.</summary>
    public string Detail { get; init; } = string.Empty;
}
