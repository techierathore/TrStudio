namespace TrStudio.Models.Contracts;

/// <summary>
/// A single unit of content presented to the safety detectors — the input text (pre-check) or the
/// produced binary (post-check). The pluggable detectors (<c>IContentClassifier</c>,
/// <c>IHashMatcher</c>, <c>IPublicFigureMatcher</c>) inspect this sample.
/// </summary>
public class SafetyContentSample
{
    /// <summary>The prompt / textual signal to classify, when available.</summary>
    public string? Text { get; set; }

    /// <summary>The produced binary to hash/classify, when available (post-check).</summary>
    public byte[]? Content { get; set; }

    /// <summary>The media kind of <see cref="Content"/> (e.g. <c>image</c>, <c>audio</c>, <c>video</c>, <c>text</c>).</summary>
    public string MediaKind { get; set; } = "text";
}
