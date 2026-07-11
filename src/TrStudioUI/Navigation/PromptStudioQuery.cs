namespace TrStudioUI.Navigation;

/// <summary>
/// Reads the <c>?prompt=</c> query the Prompt Studio (REQ-UI-081/082) carries when it sends a crafted
/// prompt into a studio. This RCL is shared by the MAUI Hybrid head and does NOT reference the ASP.NET
/// Core shared framework, so <c>QueryHelpers.ParseQuery</c> is unavailable — this parses the query
/// string with the BCL only.
/// </summary>
public static class PromptStudioQuery
{
    /// <summary>
    /// Extracts the decoded <c>prompt</c> query-string value from an absolute URI, or null when absent.
    /// </summary>
    /// <param name="aUri">The current absolute URI (e.g. <c>NavigationManager.Uri</c>).</param>
    /// <returns>The decoded prompt text, or null when there is no <c>prompt</c> parameter.</returns>
    public static string? ReadPrompt(string aUri)
    {
        if (string.IsNullOrWhiteSpace(aUri) || !Uri.TryCreate(aUri, UriKind.Absolute, out var vParsed))
        {
            return null;
        }

        var vQuery = vParsed.Query;
        if (string.IsNullOrEmpty(vQuery))
        {
            return null;
        }

        foreach (var vPair in vQuery.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var vEquals = vPair.IndexOf('=');
            var vKey = vEquals >= 0 ? vPair[..vEquals] : vPair;
            if (!string.Equals(vKey, "prompt", StringComparison.Ordinal))
            {
                continue;
            }

            var vValue = vEquals >= 0 ? vPair[(vEquals + 1)..] : string.Empty;
            return Uri.UnescapeDataString(vValue.Replace('+', ' '));
        }

        return null;
    }
}
