using TrStudio.Models.Contracts;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Safety;

/// <summary>
/// The conservative default <see cref="IPublicFigureMatcher"/> (SAFE-05). The real face-recognition
/// model is NOT installed here; this default flags the Development test sentinel and reports the
/// authoritative source as unavailable. Because public-figure impersonation is BLOCKED by default,
/// the pipeline requires an explicit recorded rights record before allowing a referenced public
/// figure — an unavailable/uncertain matcher never clears one (fail closed). The real matcher plugs
/// in by replacing this registration.
/// </summary>
public class HeuristicPublicFigureMatcher : IPublicFigureMatcher
{
    private readonly SafetyOptions objOptions;

    /// <summary>
    /// Initialises the matcher.
    /// </summary>
    /// <param name="aOptions">Safety options (test seam awareness).</param>
    public HeuristicPublicFigureMatcher(SafetyOptions aOptions)
    {
        objOptions = aOptions;
    }

    /// <inheritdoc />
    public Task<PublicFigureMatch> MatchAsync(SafetyContentSample aSample, CancellationToken aCancellationToken = default)
    {
        var vText = aSample.Text ?? string.Empty;

        if (objOptions.TestSeamEnabled && vText.Contains(SafetyOptions.PublicFigureSentinel, StringComparison.Ordinal))
        {
            return Task.FromResult(new PublicFigureMatch
            {
                SourceAvailable = false,
                IsPublicFigure = true,
                Reference = "test-seam:public-figure",
                Detail = "test-seam:pubfig"
            });
        }

        // Deferred authoritative model: report no positive match but flag the source as unavailable so
        // the pipeline treats any EXPLICITLY referenced public figure (params flag) as block-by-default.
        return Task.FromResult(new PublicFigureMatch
        {
            SourceAvailable = false,
            IsPublicFigure = false,
            Detail = "public-figure-matcher-unavailable"
        });
    }
}
