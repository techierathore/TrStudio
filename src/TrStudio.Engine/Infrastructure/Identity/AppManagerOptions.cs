namespace TrStudio.Engine.Infrastructure.Identity;

/// <summary>
/// Configuration for the AppManager child-app integration (config section <c>AppManager</c>).
/// The API key/secret pair is created in the AppManager admin UI under this application's
/// settings; credentials are read via <c>IConfiguration</c> and never logged (NFR-05).
/// </summary>
public class AppManagerOptions
{
    /// <summary>The configuration section name these options bind from.</summary>
    public const string SectionName = "AppManager";

    /// <summary>Base URL of the AppManager API (local dev: <c>https://localhost:5101/</c>).</summary>
    public string BaseUrl { get; set; } = "https://localhost:5101/";

    /// <summary>Child-app API key sent as <c>X-Api-Key</c> (resolves the ApplicationId).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Child-app API secret sent as <c>X-Api-Secret</c>.</summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>Explicit AppManager ApplicationId, used when no API key pair is configured.</summary>
    public int? ApplicationId { get; set; }

    /// <summary>Optional symmetric signing key for locally-validated dev tokens.</summary>
    public string JwtSigningKey { get; set; } = string.Empty;

    /// <summary>Whether to accept an invalid TLS certificate — honoured for loopback hosts only (local AppManager uses a self-signed cert).</summary>
    public bool AllowInvalidCert { get; set; }

    /// <summary>Whether locally-signed symmetric dev tokens are still accepted (keeps dev smokes working alongside AppManager tokens).</summary>
    public bool AllowDevSymmetricTokens { get; set; } = true;

    /// <summary>Whether bearer tokens are validated remotely via <c>POST /AuthSvc/validate</c> (the guide's documented contract).</summary>
    public bool UseRemoteTokenValidation { get; set; } = true;

    /// <summary>The pre-existing generic superadmin email; maps to the Owner role and is never created locally.</summary>
    public string SuperAdminEmail { get; set; } = "admin@appmanager.local";

    /// <summary>Personal (single-owner) mode: all execution tiers are available without license gating.</summary>
    public bool PersonalMode { get; set; } = true;

    /// <summary>AppManager feature code gating the High (serverless GPU) tier in multi-user mode.</summary>
    public string HighTierFeatureCode { get; set; } = "TIER_HIGH";

    /// <summary>AppManager feature code gating the Premium (commercial API) tier in multi-user mode.</summary>
    public string PremiumTierFeatureCode { get; set; } = "TIER_PREMIUM";
}
