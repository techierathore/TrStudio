using Microsoft.Extensions.Logging;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Safety;

/// <summary>
/// The default, non-disableable <see cref="ISafetyPipeline"/> (SAFE-01..06 / BRD-60..65). Runs the
/// floors in order on EVERY provider, on both the input (pre-check) and the output (post-check),
/// and records EVERY decision — allow or block — in the audit trail (SAFE-06). The floors are
/// enforced regardless of any mode, including the owner-only Unrestricted (NSFW) mode: that mode may
/// lift ONLY the general SFW filter, and only for fully-synthetic adult (18+) content. Every
/// uncertain or unavailable check fails closed (blocks).
/// </summary>
public class SafetyPipeline : ISafetyPipeline
{
    private readonly IContentClassifier objClassifier;
    private readonly IHashMatcher objHashMatcher;
    private readonly IPublicFigureMatcher objPublicFigureMatcher;
    private readonly IConsentService objConsentService;
    private readonly IPublicFigureRightsRepository objRightsRepository;
    private readonly ISafetyPolicyService objPolicyService;
    private readonly ISafetyAuditRepository objAuditRepository;
    private readonly ILogger<SafetyPipeline> objLogger;

    /// <summary>
    /// Initialises the pipeline.
    /// </summary>
    /// <param name="aClassifier">Pluggable content classifier (SAFE-01/02).</param>
    /// <param name="aHashMatcher">Pluggable known-bad hash matcher (SAFE-01).</param>
    /// <param name="aPublicFigureMatcher">Pluggable public-figure matcher (SAFE-05).</param>
    /// <param name="aConsentService">Shared face/voice consent gate (SAFE-03).</param>
    /// <param name="aRightsRepository">Public-figure rights records (SAFE-05).</param>
    /// <param name="aPolicyService">NSFW-mode policy (F-NSFW; never lifts a floor).</param>
    /// <param name="aAuditRepository">Audit-trail persistence (SAFE-06).</param>
    /// <param name="aLogger">Structured logger (records decisions, never the flagged content).</param>
    public SafetyPipeline(
        IContentClassifier aClassifier,
        IHashMatcher aHashMatcher,
        IPublicFigureMatcher aPublicFigureMatcher,
        IConsentService aConsentService,
        IPublicFigureRightsRepository aRightsRepository,
        ISafetyPolicyService aPolicyService,
        ISafetyAuditRepository aAuditRepository,
        ILogger<SafetyPipeline> aLogger)
    {
        objClassifier = aClassifier;
        objHashMatcher = aHashMatcher;
        objPublicFigureMatcher = aPublicFigureMatcher;
        objConsentService = aConsentService;
        objRightsRepository = aRightsRepository;
        objPolicyService = aPolicyService;
        objAuditRepository = aAuditRepository;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<SafetyDecision> PreCheckAsync(SafetyEvaluationContext aContext, CancellationToken aCancellationToken = default)
    {
        var vSignals = SafetySignals.Parse(aContext.ParamsJson);
        var vSample = new SafetyContentSample { Text = vSignals.Prompt, MediaKind = "text" };
        var vDecision = await EvaluateAsync(aContext, vSignals, vSample, SafetyPhase.PreCheck, aCancellationToken).ConfigureAwait(false);
        await AuditAsync(aContext, SafetyPhase.PreCheck, vDecision, aCancellationToken).ConfigureAwait(false);
        return vDecision;
    }

    /// <inheritdoc />
    public async Task<SafetyDecision> PostCheckAsync(SafetyEvaluationContext aContext, SafetyContentSample aOutput, CancellationToken aCancellationToken = default)
    {
        var vSignals = SafetySignals.Parse(aContext.ParamsJson);

        // The output sample carries the produced binary; also carry the prompt so text-driven
        // detectors (and the output test sentinel) evaluate the full generation context.
        aOutput.Text ??= vSignals.Prompt;
        var vDecision = await EvaluateAsync(aContext, vSignals, aOutput, SafetyPhase.PostCheck, aCancellationToken).ConfigureAwait(false);
        await AuditAsync(aContext, SafetyPhase.PostCheck, vDecision, aCancellationToken).ConfigureAwait(false);
        return vDecision;
    }

    /// <summary>
    /// Runs the floors in order. The first floor to reject returns a block; otherwise allow.
    /// </summary>
    private async Task<SafetyDecision> EvaluateAsync(
        SafetyEvaluationContext aContext,
        SafetySignals aSignals,
        SafetyContentSample aSample,
        SafetyPhase aPhase,
        CancellationToken aCancellationToken)
    {
        // ---- SAFE-01: CSAM / minors — hash match + classifier. Fail closed on any error. ----
        ContentClassification vClass;
        try
        {
            vClass = await objClassifier.ClassifyAsync(aSample, aPhase, aCancellationToken).ConfigureAwait(false);
        }
        catch (Exception vException)
        {
            objLogger.LogError(vException, "Content classifier failed for job {JobId}; failing closed (SAFE-01)", aContext.JobId);
            return SafetyDecision.Block(SafetyFloor.Csam, "classifier-error",
                "Blocked — the safety classifier was unavailable and this request fails closed (SAFE-01).");
        }

        if (vClass.Csam || vClass.MinorSexual || (aSignals.DepictsMinor && (aSignals.NsfwRequested || vClass.AdultSexual)))
        {
            return SafetyDecision.Block(SafetyFloor.Csam, "csam-or-minor",
                "Blocked — content indicating a minor in a sexual context is prohibited and non-negotiable (SAFE-01). This event is logged.");
        }

        if (aSample.Content is { Length: > 0 })
        {
            HashMatchResult vHash;
            try
            {
                vHash = await objHashMatcher.MatchAsync(aSample.Content, aCancellationToken).ConfigureAwait(false);
            }
            catch (Exception vException)
            {
                objLogger.LogError(vException, "Hash matcher failed for job {JobId}; failing closed (SAFE-01)", aContext.JobId);
                return SafetyDecision.Block(SafetyFloor.Csam, "hash-error",
                    "Blocked — the CSAM hash source was unavailable and this output fails closed (SAFE-01).");
            }

            if (vHash.IsMatch)
            {
                return SafetyDecision.Block(SafetyFloor.Csam, "hash-match",
                    "Blocked — output matched a known-bad hash (SAFE-01). This event is logged.");
            }
        }

        // ---- SAFE-02: NCII / sexualisation / face-swap of a real identifiable person. ----
        var vRealPerson = aSignals.IsRealPerson || aSignals.PhotorealOfRealPerson;
        if (vRealPerson && (aSignals.NsfwRequested || vClass.RealPersonSexual || vClass.AdultSexual))
        {
            return SafetyDecision.Block(SafetyFloor.RealPersonSexual, "real-person-sexual",
                "Blocked — sexual or NCII content depicting a real, identifiable person is refused (SAFE-02).");
        }

        // ---- SAFE-03: recorded-consent gate for any real-person face/voice work. Fail closed. ----
        if (aSignals.IsRealPerson)
        {
            if (string.IsNullOrWhiteSpace(aSignals.SubjectReference))
            {
                return SafetyDecision.Block(SafetyFloor.Consent, "no-subject",
                    "Blocked — real-person work requires a subjectReference and recorded consent evidence (SAFE-03).");
            }

            var vModality = SafetySignals.ModalityForTask(aContext.Task);
            var vHasConsent = await objConsentService
                .HasConsentAsync(aContext.UserId, aSignals.SubjectReference, vModality, aCancellationToken)
                .ConfigureAwait(false);
            if (!vHasConsent)
            {
                return SafetyDecision.Block(SafetyFloor.Consent, "no-consent",
                    "Blocked — no recorded consent evidence for this real subject. Record consent first (SAFE-03).");
            }
        }

        // ---- SAFE-05: public-figure impersonation — blocked by default, allowed only with rights. ----
        PublicFigureMatch vPublicFigure;
        try
        {
            vPublicFigure = await objPublicFigureMatcher.MatchAsync(aSample, aCancellationToken).ConfigureAwait(false);
        }
        catch (Exception vException)
        {
            objLogger.LogError(vException, "Public-figure matcher failed for job {JobId}; failing closed (SAFE-05)", aContext.JobId);
            return SafetyDecision.Block(SafetyFloor.PublicFigure, "matcher-error",
                "Blocked — the public-figure matcher was unavailable and this request fails closed (SAFE-05).");
        }

        var vReferencesPublicFigure = aSignals.IsPublicFigure || vPublicFigure.IsPublicFigure;
        if (vReferencesPublicFigure)
        {
            var vReference = aSignals.PublicFigureReference ?? vPublicFigure.Reference ?? aSignals.SubjectReference;
            var vHasRights = !string.IsNullOrWhiteSpace(vReference)
                && await objRightsRepository.HasRightsAsync(aContext.UserId, vReference, aCancellationToken).ConfigureAwait(false);
            if (!vHasRights)
            {
                return SafetyDecision.Block(SafetyFloor.PublicFigure, "no-rights",
                    "Blocked — impersonating a real public figure requires an explicit recorded rights record (SAFE-05).");
            }
        }

        // ---- General SFW filter + NSFW scope (F-NSFW / BRD-79/80). The ONLY toggleable gate. ----
        if (aSignals.NsfwRequested || vClass.AdultSexual)
        {
            var vPolicy = await objPolicyService.GetNsfwPolicyAsync(aCancellationToken).ConfigureAwait(false);
            if (!vPolicy.EffectiveNsfwEnabled)
            {
                return SafetyDecision.Block(SafetyFloor.GeneralSfwFilter, "nsfw-off",
                    "Blocked by the general SFW filter — adult content requires the owner-only Unrestricted (NSFW) mode, which is off (F-NSFW).");
            }

            // Mode ON: enforce scope — fully-synthetic adult (18+) personas ONLY. Photoreal-of-real,
            // real-person references and minors are refused even with the mode on (BRD-80).
            if (aSignals.PhotorealOfRealPerson || aSignals.IsRealPerson || aSignals.DepictsMinor
                || !aSignals.IsSynthetic || !aSignals.IsAdult)
            {
                return SafetyDecision.Block(SafetyFloor.NsfwScope, "nsfw-scope",
                    "Blocked — Unrestricted (NSFW) mode is limited to fully-synthetic, adult (18+) personas; real-person, photoreal-of-real and minor content are refused (BRD-80).");
            }
        }

        return SafetyDecision.Allow();
    }

    private Task AuditAsync(SafetyEvaluationContext aContext, SafetyPhase aPhase, SafetyDecision aDecision, CancellationToken aCancellationToken)
    {
        if (!aDecision.Allowed)
        {
            objLogger.LogWarning(
                "SAFETY BLOCK ({Floor}/{Reason}) at {Phase} for user {UserId} model {ModelId} job {JobId}",
                aDecision.Floor, aDecision.Reason, aPhase, aContext.UserId, aContext.ModelId, aContext.JobId);
        }

        return objAuditRepository.InsertAsync(new SafetyAuditEntry
        {
            AuditId = Guid.NewGuid(),
            JobId = aContext.JobId == Guid.Empty ? null : aContext.JobId,
            UserId = aContext.UserId,
            ModelId = aContext.ModelId,
            Phase = aPhase,
            Allowed = aDecision.Allowed,
            Floor = aDecision.Floor,
            Reason = aDecision.Reason,
            Detail = aDecision.Message,
            CreatedUtc = DateTime.UtcNow
        }, aCancellationToken);
    }
}
