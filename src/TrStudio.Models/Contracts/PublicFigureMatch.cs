namespace TrStudio.Models.Contracts;

/// <summary>
/// The result of matching content/prompt against a public-figure identity source (SAFE-05). The
/// real face-recognition model is pluggable; an unavailable source is treated conservatively —
/// a referenced public figure without a rights record is blocked by default (fail closed).
/// </summary>
public class PublicFigureMatch
{
    /// <summary>Whether the authoritative public-figure matcher is installed and produced this result.</summary>
    public bool SourceAvailable { get; init; }

    /// <summary>Whether the content/prompt references a real public figure.</summary>
    public bool IsPublicFigure { get; init; }

    /// <summary>A reference identifying the matched public figure, when known.</summary>
    public string? Reference { get; init; }

    /// <summary>A short, non-revealing detail for the audit trail.</summary>
    public string Detail { get; init; } = string.Empty;
}
