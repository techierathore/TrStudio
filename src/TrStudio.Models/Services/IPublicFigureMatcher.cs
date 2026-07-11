using TrStudio.Models.Contracts;

namespace TrStudio.Models.Services;

/// <summary>
/// Pluggable public-figure identity matcher (SAFE-05). The real face-recognition model plugs in via
/// config; the default implementation is a conservative heuristic. Public-figure impersonation is
/// blocked by default, so an unavailable/uncertain matcher never clears a referenced public figure
/// (fail closed).
/// </summary>
public interface IPublicFigureMatcher
{
    /// <summary>
    /// Determines whether a content sample references a real public figure.
    /// </summary>
    /// <param name="aSample">The text or binary to match.</param>
    /// <param name="aCancellationToken">Token to cancel the operation.</param>
    /// <returns>The match result; a referenced public figure requires a recorded rights record.</returns>
    Task<PublicFigureMatch> MatchAsync(SafetyContentSample aSample, CancellationToken aCancellationToken = default);
}
