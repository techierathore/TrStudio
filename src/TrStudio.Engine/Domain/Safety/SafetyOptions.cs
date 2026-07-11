using Microsoft.Extensions.Configuration;

namespace TrStudio.Engine.Domain.Safety;

/// <summary>
/// Options for the safety pipeline (SAFE-01..06). The real detection models (CSAM hash DB, NCII
/// classifier, public-figure face recognition) plug in via configuration; until then the pipeline
/// runs a conservative fail-closed default. The Development-only test seam lets an automated smoke
/// force a flagged input via well-known sentinels — it is a TEST HOOK, never a bypass, and is off
/// outside Development.
/// </summary>
public class SafetyOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Safety";

    /// <summary>Sentinel that forces a CSAM/minor classification (Development test seam only).</summary>
    public const string CsamSentinel = "__CSAM_TEST__";

    /// <summary>Sentinel that forces a minor-sexual classification (Development test seam only).</summary>
    public const string MinorSentinel = "__MINOR_TEST__";

    /// <summary>Sentinel that forces an NCII / real-person-sexual classification (Development test seam only).</summary>
    public const string NciiSentinel = "__NCII_TEST__";

    /// <summary>Sentinel that forces a public-figure match (Development test seam only).</summary>
    public const string PublicFigureSentinel = "__PUBFIG_TEST__";

    /// <summary>Sentinel that passes pre-check but is flagged on the OUTPUT post-check (Development test seam only).</summary>
    public const string OutputBlockSentinel = "__OUTPUT_BLOCK_TEST__";

    /// <summary>
    /// Whether the Development-only sentinel test seam is active. Wired to
    /// <c>IHostEnvironment.IsDevelopment()</c> in the API host; NEVER enabled in production.
    /// </summary>
    public bool TestSeamEnabled { get; set; }

    /// <summary>
    /// The HMAC key used to sign the C2PA-style provenance manifest so it is non-trivial to strip
    /// silently (SAFE-04). Read from <c>Safety:ProvenanceSigningKey</c>; a deployment default is
    /// used when unset.
    /// </summary>
    public string ProvenanceSigningKey { get; set; } = "TrStudioProvenanceSigningKey-Dev-ReplaceInProduction";

    /// <summary>
    /// Builds options from configuration.
    /// </summary>
    /// <param name="aConfiguration">The application configuration (section <c>Safety</c>).</param>
    /// <param name="aIsDevelopment">Whether the host is running in the Development environment.</param>
    /// <returns>The bound options.</returns>
    public static SafetyOptions FromConfiguration(IConfiguration aConfiguration, bool aIsDevelopment)
    {
        var vSection = aConfiguration.GetSection(SectionName);
        var vSigningKey = vSection["ProvenanceSigningKey"];
        return new SafetyOptions
        {
            // The seam is only ever on in Development, and only when not explicitly disabled there.
            TestSeamEnabled = aIsDevelopment && vSection.GetValue("TestSeamEnabled", true),
            ProvenanceSigningKey = string.IsNullOrWhiteSpace(vSigningKey)
                ? "TrStudioProvenanceSigningKey-Dev-ReplaceInProduction"
                : vSigningKey
        };
    }
}
