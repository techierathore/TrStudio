namespace TrStudio.Engine.Domain.Venice;

/// <summary>
/// Venice's address and key as read from the database for one call (ADR-014, BRD-93/98). Never
/// logged; the key comes from the AES-GCM key store and is never returned by any endpoint.
/// </summary>
/// <param name="BaseUrl">The API base address entered on Registry &amp; providers.</param>
/// <param name="ApiKey">The decrypted bearer key.</param>
public sealed record VeniceConnection(string BaseUrl, string ApiKey)
{
    /// <summary>Hides the key from record printing so it can never leak into a log line.</summary>
    /// <returns>The address only.</returns>
    public override string ToString() => $"VeniceConnection {{ BaseUrl = {BaseUrl} }}";
}
