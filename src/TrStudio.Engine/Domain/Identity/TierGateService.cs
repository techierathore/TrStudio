using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrStudio.Engine.Infrastructure.Identity;
using TrStudio.Models.Contracts;
using TrStudio.Models.Enums;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Domain.Identity;

/// <summary>
/// Server-side execution tier gate (BRD-59, REQ-FN-014): Fast (local) is always available; in
/// personal mode all tiers are available; in multi-user mode the High/Premium tiers require the
/// corresponding AppManager feature (FeatureSvc), and a refusal carries an upgrade prompt.
/// </summary>
public class TierGateService : ITierGate
{
    private readonly IAppManagerClient objAppManagerClient;
    private readonly AppManagerOptions objOptions;
    private readonly ILogger<TierGateService> objLogger;

    /// <summary>
    /// Initialises the gate over the AppManager client.
    /// </summary>
    /// <param name="aAppManagerClient">Client used for FeatureSvc checks.</param>
    /// <param name="aOptions">The AppManager integration options (mode and feature codes).</param>
    /// <param name="aLogger">Logger for gate decisions.</param>
    public TierGateService(
        IAppManagerClient aAppManagerClient,
        IOptions<AppManagerOptions> aOptions,
        ILogger<TierGateService> aLogger)
    {
        objAppManagerClient = aAppManagerClient;
        objOptions = aOptions.Value;
        objLogger = aLogger;
    }

    /// <inheritdoc />
    public async Task<TierGateResult> CheckTierAsync(string aUserId, GenerationTier aTier, string? aAccessToken = null, CancellationToken aCancellationToken = default)
    {
        if (aTier == GenerationTier.Fast || objOptions.PersonalMode)
        {
            return Allowed(aTier);
        }

        var vFeatureCode = aTier == GenerationTier.High
            ? objOptions.HighTierFeatureCode
            : objOptions.PremiumTierFeatureCode;

        var vHasAccess = !string.IsNullOrEmpty(aAccessToken)
            ? await objAppManagerClient.IsFeatureEnabledForTokenAsync(aAccessToken, vFeatureCode, aCancellationToken).ConfigureAwait(false)
            : await objAppManagerClient.IsFeatureEnabledAsync(aUserId, vFeatureCode, aCancellationToken).ConfigureAwait(false);

        if (vHasAccess)
        {
            return Allowed(aTier);
        }

        objLogger.LogInformation("Tier {Tier} refused for user {UserId}: feature {Feature} not licensed.", aTier, aUserId, vFeatureCode);
        return new TierGateResult
        {
            IsAllowed = false,
            Tier = aTier,
            DeniedReason = "TIER_GATED",
            RequiredFeatureCode = vFeatureCode,
            UpgradeMessage = $"The {aTier} tier is not included in your current license. " +
                             "Upgrade your plan in AppManager to unlock it, or use the Fast (local) tier."
        };
    }

    private static TierGateResult Allowed(GenerationTier aTier)
    {
        return new TierGateResult { IsAllowed = true, Tier = aTier };
    }
}
