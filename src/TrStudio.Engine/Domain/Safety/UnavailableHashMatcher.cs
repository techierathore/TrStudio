using TrStudio.Models.Contracts;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Safety;

/// <summary>
/// The default <see cref="IHashMatcher"/> for when no known-bad perceptual-hash database is
/// installed (SAFE-01). It reports the source as UNAVAILABLE and returns no positive match — it
/// never fabricates a false negative. The pipeline combines this with the classifier and applies
/// the fail-closed rule to risk-elevated content, so a deferred hash DB cannot silently allow CSAM.
/// The real hash source plugs in by replacing this registration.
/// </summary>
public class UnavailableHashMatcher : IHashMatcher
{
    /// <inheritdoc />
    public Task<HashMatchResult> MatchAsync(byte[] aContent, CancellationToken aCancellationToken = default)
        => Task.FromResult(new HashMatchResult
        {
            SourceAvailable = false,
            IsMatch = false,
            Detail = "hash-db-unavailable"
        });
}
