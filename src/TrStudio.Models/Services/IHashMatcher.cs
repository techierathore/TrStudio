using TrStudio.Models.Contracts;

namespace TrStudio.Models.Services;

/// <summary>
/// Pluggable known-bad perceptual-hash matcher (SAFE-01, CSAM prevention by design). The real hash
/// database plugs in via config; the default implementation reports the source as unavailable and
/// never returns a false negative that would silently allow risk-elevated content (fail closed).
/// </summary>
public interface IHashMatcher
{
    /// <summary>
    /// Matches content against the known-bad hash database.
    /// </summary>
    /// <param name="aContent">The binary content to hash and match.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The match result; a positive match is a hard block.</returns>
    Task<HashMatchResult> MatchAsync(byte[] aContent, CancellationToken aCancellationToken = default);
}
