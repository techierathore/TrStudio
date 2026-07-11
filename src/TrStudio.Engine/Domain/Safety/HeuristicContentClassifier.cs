using Microsoft.Extensions.Logging;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Safety;

/// <summary>
/// The conservative fail-closed default <see cref="IContentClassifier"/> (SAFE-01/02). The real
/// CSAM/nudity model is NOT installed here; this default flags:
/// <list type="bullet">
///   <item>the Development-only test sentinels (a smoke seam, gated by <see cref="SafetyOptions.TestSeamEnabled"/>);</item>
///   <item>conservative keyword co-occurrence signals for minor-sexual and CSAM content.</item>
/// </list>
/// It reports <see cref="ContentClassification.SourceAvailable"/> = <c>false</c> so the pipeline
/// knows the authoritative model is deferred and applies the fail-closed rule to risk-elevated
/// paths. Ordinary content is reported clear so normal generation is unaffected.
/// </summary>
public class HeuristicContentClassifier : IContentClassifier
{
    private static readonly string[] MinorTerms =
        { "child", "children", "kid", "kids", "minor", "underage", "preteen", "toddler", "infant", "schoolgirl", "schoolboy" };

    private static readonly string[] SexualTerms =
        { "nude", "naked", "sexual", "sex", "porn", "explicit", "nsfw", "erotic", "xxx" };

    private readonly SafetyOptions objOptions;
    private readonly ILogger<HeuristicContentClassifier> objLogger;

    /// <summary>
    /// Initialises the classifier.
    /// </summary>
    /// <param name="aOptions">Safety options (test seam + deferred-model awareness).</param>
    /// <param name="aLogger">Structured logger (never logs the flagged content verbatim).</param>
    public HeuristicContentClassifier(SafetyOptions aOptions, ILogger<HeuristicContentClassifier> aLogger)
    {
        objOptions = aOptions;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public Task<ContentClassification> ClassifyAsync(SafetyContentSample aSample, SafetyPhase aPhase, CancellationToken aCancellationToken = default)
    {
        var vText = aSample.Text ?? string.Empty;
        var vLower = vText.ToLowerInvariant();

        // Development-only test seam: force a category from a well-known sentinel so an automated
        // smoke can prove the block fires. Never active outside Development.
        if (objOptions.TestSeamEnabled)
        {
            if (vText.Contains(SafetyOptions.CsamSentinel, StringComparison.Ordinal))
            {
                return Task.FromResult(Flag(csam: true, detail: "test-seam:csam"));
            }

            if (vText.Contains(SafetyOptions.MinorSentinel, StringComparison.Ordinal))
            {
                return Task.FromResult(Flag(minorSexual: true, detail: "test-seam:minor"));
            }

            if (vText.Contains(SafetyOptions.NciiSentinel, StringComparison.Ordinal))
            {
                return Task.FromResult(Flag(realPersonSexual: true, detail: "test-seam:ncii"));
            }

            // The output-block sentinel is deliberately IGNORED at pre-check and flagged only on the
            // output post-check, proving the post-check path blocks a job after it has run.
            if (aPhase == SafetyPhase.PostCheck && vText.Contains(SafetyOptions.OutputBlockSentinel, StringComparison.Ordinal))
            {
                return Task.FromResult(Flag(csam: true, detail: "test-seam:output-block"));
            }
        }

        // Conservative real-signal heuristic: a minor term co-occurring with a sexual term is treated
        // as minor-sexual (SAFE-01) — block. The authoritative model refines this later.
        var vHasMinor = MinorTerms.Any(aTerm => ContainsWord(vLower, aTerm));
        var vHasSexual = SexualTerms.Any(aTerm => ContainsWord(vLower, aTerm));
        if (vHasMinor && vHasSexual)
        {
            objLogger.LogWarning("Content classifier flagged minor-sexual co-occurrence at {Phase} (deferred model)", aPhase);
            return Task.FromResult(Flag(minorSexual: true, detail: "heuristic:minor-sexual"));
        }

        // A bare sexual signal is adult content (drives the toggleable general SFW filter, not a floor).
        return Task.FromResult(new ContentClassification
        {
            SourceAvailable = false,
            AdultSexual = vHasSexual,
            Detail = vHasSexual ? "heuristic:adult" : "heuristic:clear"
        });
    }

    private static ContentClassification Flag(bool csam = false, bool minorSexual = false, bool realPersonSexual = false, string detail = "")
        => new()
        {
            SourceAvailable = false,
            Csam = csam,
            MinorSexual = minorSexual,
            RealPersonSexual = realPersonSexual,
            AdultSexual = true,
            Detail = detail
        };

    private static bool ContainsWord(string aHaystack, string aWord)
    {
        var vIndex = aHaystack.IndexOf(aWord, StringComparison.Ordinal);
        while (vIndex >= 0)
        {
            var vBeforeOk = vIndex == 0 || !char.IsLetterOrDigit(aHaystack[vIndex - 1]);
            var vAfter = vIndex + aWord.Length;
            var vAfterOk = vAfter >= aHaystack.Length || !char.IsLetterOrDigit(aHaystack[vAfter]);
            if (vBeforeOk && vAfterOk)
            {
                return true;
            }

            vIndex = aHaystack.IndexOf(aWord, vIndex + 1, StringComparison.Ordinal);
        }

        return false;
    }
}
