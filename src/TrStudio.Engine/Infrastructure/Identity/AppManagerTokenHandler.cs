using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TrStudio.Models.Services;

namespace TrStudio.Engine.Infrastructure.Identity;

/// <summary>
/// JWT bearer token handler for AppManager-issued tokens (REQ-FN-012). AppManager does not
/// publish its signing key to child apps; the guide's documented contract is remote validation
/// via <c>POST /AuthSvc/validate</c>. This handler first tries local symmetric validation (the
/// dev-token fallback, behind <c>AppManager:AllowDevSymmetricTokens</c>), then asks AppManager,
/// caching positive verdicts briefly to avoid a network hop per request.
/// </summary>
public class AppManagerTokenHandler : JsonWebTokenHandler
{
    private const string ValidationCachePrefix = "AppManager:TokenValidation:";

    private readonly IServiceScopeFactory objScopeFactory;
    private readonly IMemoryCache objCache;
    private readonly AppManagerOptions objOptions;
    private readonly ILogger<AppManagerTokenHandler> objLogger;

    /// <summary>
    /// Initialises the handler.
    /// </summary>
    /// <param name="aScopeFactory">Scope factory used to resolve the AppManager client per validation.</param>
    /// <param name="aCache">In-process cache for remote validation verdicts.</param>
    /// <param name="aOptions">The AppManager integration options.</param>
    /// <param name="aLogger">Logger for validation diagnostics (never logs tokens).</param>
    public AppManagerTokenHandler(
        IServiceScopeFactory aScopeFactory,
        IMemoryCache aCache,
        IOptions<AppManagerOptions> aOptions,
        ILogger<AppManagerTokenHandler> aLogger)
    {
        objScopeFactory = aScopeFactory;
        objCache = aCache;
        objOptions = aOptions.Value;
        objLogger = aLogger;
        MapInboundClaims = true;
    }

    /// <summary>
    /// Validates a bearer token: local symmetric dev-token validation first (when enabled),
    /// then remote validation against AppManager <c>POST /AuthSvc/validate</c>.
    /// </summary>
    /// <param name="aToken">The raw JWT from the Authorization header.</param>
    /// <param name="aValidationParameters">The configured validation parameters (symmetric dev key).</param>
    /// <returns>The validation result; claims come from the token payload once AppManager attests validity.</returns>
    public override async Task<TokenValidationResult> ValidateTokenAsync(string aToken, TokenValidationParameters aValidationParameters)
    {
        TokenValidationResult? vLocalResult = null;
        if (objOptions.AllowDevSymmetricTokens)
        {
            vLocalResult = await base.ValidateTokenAsync(aToken, aValidationParameters).ConfigureAwait(false);
            if (vLocalResult.IsValid)
            {
                return vLocalResult;
            }
        }

        if (!objOptions.UseRemoteTokenValidation)
        {
            return vLocalResult ?? await base.ValidateTokenAsync(aToken, aValidationParameters).ConfigureAwait(false);
        }

        var vIsRemoteValid = await IsRemoteValidAsync(aToken).ConfigureAwait(false);
        if (!vIsRemoteValid)
        {
            return vLocalResult ?? await base.ValidateTokenAsync(aToken, aValidationParameters).ConfigureAwait(false);
        }

        return await ValidateAttestedTokenAsync(aToken, aValidationParameters).ConfigureAwait(false);
    }

    private async Task<TokenValidationResult> ValidateAttestedTokenAsync(string aToken, TokenValidationParameters aValidationParameters)
    {
        // AppManager has attested the token's signature/session; re-run local validation with
        // the signature check relaxed so lifetime checks and claim extraction still apply.
        var vRelaxedParameters = aValidationParameters.Clone();
        vRelaxedParameters.ValidateIssuerSigningKey = false;
        vRelaxedParameters.RequireSignedTokens = false;
        vRelaxedParameters.SignatureValidator = (aRawToken, aParameters) => new JsonWebToken(aRawToken);
        return await base.ValidateTokenAsync(aToken, vRelaxedParameters).ConfigureAwait(false);
    }

    private async Task<bool> IsRemoteValidAsync(string aToken)
    {
        var vCacheKey = ValidationCachePrefix + ComputeTokenHash(aToken);
        if (objCache.TryGetValue(vCacheKey, out bool vCachedVerdict))
        {
            return vCachedVerdict;
        }

        var vIsValid = await ValidateAgainstAppManagerAsync(aToken).ConfigureAwait(false);
        objCache.Set(vCacheKey, vIsValid, vIsValid ? TimeSpan.FromSeconds(60) : TimeSpan.FromSeconds(15));
        return vIsValid;
    }

    private async Task<bool> ValidateAgainstAppManagerAsync(string aToken)
    {
        try
        {
            using var vScope = objScopeFactory.CreateScope();
            var vClient = vScope.ServiceProvider.GetRequiredService<IAppManagerClient>();
            return await vClient.ValidateTokenAsync(aToken).ConfigureAwait(false);
        }
        catch (Exception vException)
        {
            objLogger.LogWarning(vException, "Remote token validation against AppManager failed; treating token as invalid.");
            return false;
        }
    }

    private static string ComputeTokenHash(string aToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(aToken)));
    }
}
