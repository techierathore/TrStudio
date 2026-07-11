using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Safety;

/// <summary>
/// Default <see cref="ISafetyPolicyService"/> (SAFE-06 / F-NSFW / BRD-79). Governs the owner-only
/// Unrestricted (NSFW) mode: OFF BY DEFAULT, personal/single-user mode ONLY, and AUTO-DISABLED in
/// multi-user/SaaS. The effective mode is always <c>stored AND personalMode</c>, so a stored
/// "enabled" flag is forced off the moment the deployment is not personal-mode. This service can
/// NEVER lift the non-disableable floors (SAFE-01..06) — it only ever governs the general SFW filter.
/// </summary>
public class SafetyPolicyService : ISafetyPolicyService
{
    private readonly ISafetyPolicyRepository objRepository;
    private readonly bool objPersonalMode;
    private readonly ILogger<SafetyPolicyService> objLogger;

    /// <summary>
    /// Initialises the service.
    /// </summary>
    /// <param name="aRepository">Policy persistence (single-row per deployment).</param>
    /// <param name="aConfiguration">Application configuration (<c>AppManager:PersonalMode</c>).</param>
    /// <param name="aLogger">Structured logger.</param>
    public SafetyPolicyService(ISafetyPolicyRepository aRepository, IConfiguration aConfiguration, ILogger<SafetyPolicyService> aLogger)
    {
        objRepository = aRepository;
        // Personal-mode gates whether the toggle can ever be effective. Multi-user/SaaS forces it off.
        objPersonalMode = aConfiguration.GetValue("AppManager:PersonalMode", true);
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<NsfwPolicyResponse> GetNsfwPolicyAsync(CancellationToken aCancellationToken = default)
    {
        var vPolicy = await objRepository.GetAsync(aCancellationToken).ConfigureAwait(false);
        var vStored = vPolicy?.NsfwModeEnabled ?? false;
        return BuildResponse(vStored);
    }

    /// <inheritdoc />
    public async Task<NsfwPolicyResponse> SetNsfwEnabledAsync(bool aEnabled, CancellationToken aCancellationToken = default)
    {
        // Enabling is refused in multi-user/SaaS mode — the toggle is forced off and never persisted true.
        if (aEnabled && !objPersonalMode)
        {
            objLogger.LogWarning("NSFW mode enable REFUSED: deployment is not in personal/single-user mode (F-NSFW auto-off).");
            await PersistAsync(false, aCancellationToken).ConfigureAwait(false);
            return BuildResponse(false, aRefused: true);
        }

        await PersistAsync(aEnabled, aCancellationToken).ConfigureAwait(false);
        objLogger.LogInformation("NSFW mode set to {Enabled} (personalMode={PersonalMode}).", aEnabled, objPersonalMode);
        return BuildResponse(aEnabled);
    }

    private Task PersistAsync(bool aStored, CancellationToken aCancellationToken)
        => objRepository.UpsertAsync(new SafetyPolicy
        {
            PolicyKey = "default",
            NsfwModeEnabled = aStored,
            UpdatedUtc = DateTime.UtcNow
        }, aCancellationToken);

    private NsfwPolicyResponse BuildResponse(bool aStored, bool aRefused = false)
    {
        var vEffective = aStored && objPersonalMode;
        return new NsfwPolicyResponse
        {
            StoredNsfwEnabled = aStored,
            PersonalMode = objPersonalMode,
            EffectiveNsfwEnabled = vEffective,
            Message = aRefused
                ? "Refused — Unrestricted (NSFW) mode is only available in personal/single-user mode; it is auto-disabled in multi-user/SaaS (F-NSFW). The non-disableable floors remain enforced."
                : vEffective
                    ? "Unrestricted (NSFW) mode is ON for fully-synthetic adult (18+) personas only. The non-disableable floors (SAFE-01..06) remain enforced."
                    : objPersonalMode
                        ? "Unrestricted (NSFW) mode is OFF (default). The general SFW filter is active."
                        : "Unrestricted (NSFW) mode is forced OFF in multi-user/SaaS mode (F-NSFW). The general SFW filter is active."
        };
    }
}
