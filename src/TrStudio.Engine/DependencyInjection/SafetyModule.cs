using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrStudio.Engine.Domain.Safety;
using TrStudio.Engine.Infrastructure.Data;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.DependencyInjection;

/// <summary>
/// DI registrations for the non-disableable safety floors (SAFE-01..06 / FN-056..063): the safety
/// pipeline, the pluggable fail-closed detectors (CSAM hash matcher, content classifier,
/// public-figure matcher), the shared face/voice consent gate, provenance stamping, the NSFW-mode
/// policy service, abuse reporting and the audit trail. The real detection models plug in later by
/// replacing the default detector registrations; nothing here is toggleable.
/// </summary>
public static class SafetyModule
{
    /// <summary>
    /// Registers <see cref="SafetyOptions"/>, the safety pipeline, the fail-closed default detectors,
    /// the consent/policy/abuse services and their Dapper repositories.
    /// </summary>
    /// <param name="aServices">The service collection to register into.</param>
    /// <param name="aConfiguration">The application configuration (section <c>Safety</c>).</param>
    /// <param name="aIsDevelopment">Whether the host runs in Development (gates the sentinel test seam).</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddSafetyModule(this IServiceCollection aServices, IConfiguration aConfiguration, bool aIsDevelopment)
    {
        aServices.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();

        // Options — the sentinel test seam is only ever active in Development.
        aServices.AddSingleton(SafetyOptions.FromConfiguration(aConfiguration, aIsDevelopment));

        // Pluggable detectors — conservative fail-closed defaults. Replace these registrations to
        // plug in the real CSAM hash DB / NCII classifier / public-figure face-recognition models.
        aServices.TryAddSingleton<IContentClassifier, HeuristicContentClassifier>();
        aServices.TryAddSingleton<IHashMatcher, UnavailableHashMatcher>();
        aServices.TryAddSingleton<IPublicFigureMatcher, HeuristicPublicFigureMatcher>();

        // Repositories (SAFE-03/05/06).
        aServices.TryAddSingleton<ISafetyAuditRepository, SafetyAuditRepository>();
        aServices.TryAddSingleton<IAbuseReportRepository, AbuseReportRepository>();
        aServices.TryAddSingleton<IConsentRepository, ConsentRepository>();
        aServices.TryAddSingleton<IPublicFigureRightsRepository, PublicFigureRightsRepository>();
        aServices.TryAddSingleton<ISafetyPolicyRepository, SafetyPolicyRepository>();

        // Services.
        aServices.TryAddSingleton<IConsentService, ConsentService>();
        aServices.TryAddSingleton<ISafetyPolicyService, SafetyPolicyService>();
        aServices.TryAddSingleton<IAbuseReportService, AbuseReportService>();
        aServices.TryAddSingleton<IProvenanceStamper, ProvenanceStamper>();

        // The pipeline itself — wired into the orchestrator (pre-check) and worker (post-check).
        aServices.TryAddSingleton<ISafetyPipeline, SafetyPipeline>();

        return aServices;
    }
}
