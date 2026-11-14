namespace TrStudio.Engine.Infrastructure.Identity;

/// <summary>
/// The headers a desktop app sends to AppManager (phase 3, ADR-019): its application id and one
/// API key. There is no API secret header.
/// </summary>
public static class AppManagerHeaders
{
    /// <summary>The application id header.</summary>
    public const string ApplicationId = "X-App-Id";

    /// <summary>The API key header.</summary>
    public const string ApiKey = "X-Api-Key";
}
