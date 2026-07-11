using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;

namespace TrStudio.Models.Services;

/// <summary>
/// Pluggable content-safety classifier (SAFE-01/02). The real CSAM/nudity model plugs in via
/// config; the default implementation is a conservative fail-closed heuristic. A classifier that
/// throws or is unavailable must never cause risk-elevated content to be allowed — the pipeline
/// fails closed.
/// </summary>
public interface IContentClassifier
{
    /// <summary>
    /// Classifies a content sample for prohibited-floor categories.
    /// </summary>
    /// <param name="aSample">The text or binary to classify.</param>
    /// <param name="aPhase">Whether this is a pre-check (input) or post-check (output) evaluation.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The classification result; the caller blocks on any prohibited category.</returns>
    Task<ContentClassification> ClassifyAsync(SafetyContentSample aSample, SafetyPhase aPhase, CancellationToken aCancellationToken = default);
}
