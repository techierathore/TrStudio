namespace TrStudioApi.Infrastructure.Auth;

/// <summary>
/// Scheme/header constants for the external <c>X-Api-Key</c> authentication seam (REQ-FN-069/BRD-66).
/// </summary>
public static class ApiKeyDefaults
{
    /// <summary>The authentication scheme name for API-key auth.</summary>
    public const string Scheme = "ApiKey";

    /// <summary>The request header carrying the API key.</summary>
    public const string HeaderName = "X-Api-Key";

    /// <summary>
    /// The composite policy-scheme name that routes each request to EITHER the JWT bearer scheme or
    /// the API-key scheme, so job endpoints accept both without weakening the JWT path.
    /// </summary>
    public const string SmartScheme = "SmartAuth";
}
