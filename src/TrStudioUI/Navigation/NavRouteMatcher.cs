namespace TrStudioUI.Navigation;

/// <summary>
/// Resolves which navigation item is active for the current route.
/// Longest matching prefix wins, so <c>/avatar/characters</c> activates
/// Characters rather than Avatar Studio.
/// </summary>
public static class NavRouteMatcher
{
    /// <summary>
    /// Finds the navigation item that best matches the given application-relative path.
    /// </summary>
    /// <param name="aConfig">The host navigation configuration.</param>
    /// <param name="aRelativePath">Relative path from <c>NavigationManager.ToBaseRelativePath</c> (may include query/hash).</param>
    /// <returns>The best-matching item, or null when nothing matches (e.g. the home route).</returns>
    public static AppNavItem? FindActive(AppNavigationConfig aConfig, string aRelativePath)
    {
        ArgumentNullException.ThrowIfNull(aConfig);

        var vPath = Normalize(aRelativePath);
        AppNavItem? vBest = null;
        var vBestLength = -1;

        foreach (var vGroup in aConfig.Groups)
        {
            foreach (var vItem in vGroup.Items)
            {
                var vPrefixes = new List<string> { vItem.Href };
                if (vItem.ExtraMatchPrefixes is not null)
                {
                    vPrefixes.AddRange(vItem.ExtraMatchPrefixes);
                }

                foreach (var vPrefix in vPrefixes)
                {
                    var vNormalizedPrefix = Normalize(vPrefix);
                    if (!Matches(vPath, vNormalizedPrefix))
                    {
                        continue;
                    }

                    // Prefer the most specific (longest) primary Href; extra prefixes
                    // count with their own length so deeper items still win.
                    if (vNormalizedPrefix.Length > vBestLength)
                    {
                        vBestLength = vNormalizedPrefix.Length;
                        vBest = vItem;
                    }
                }
            }
        }

        return vBest;
    }

    /// <summary>Normalizes a route to a lower-case, leading-slash, no-trailing-slash path without query/hash.</summary>
    /// <param name="aPath">Raw path or href.</param>
    /// <returns>The normalized path (empty string for the root).</returns>
    private static string Normalize(string aPath)
    {
        var vPath = aPath ?? string.Empty;
        var vQueryIndex = vPath.IndexOfAny(['?', '#']);
        if (vQueryIndex >= 0)
        {
            vPath = vPath[..vQueryIndex];
        }

        vPath = "/" + vPath.Trim('/');
        return vPath == "/" ? string.Empty : vPath.ToLowerInvariant();
    }

    /// <summary>Checks whether a normalized path equals a prefix or sits underneath it.</summary>
    /// <param name="aPath">Normalized current path.</param>
    /// <param name="aPrefix">Normalized candidate prefix.</param>
    /// <returns>True when the prefix matches at a segment boundary.</returns>
    private static bool Matches(string aPath, string aPrefix)
    {
        if (aPrefix.Length == 0)
        {
            return aPath.Length == 0;
        }

        return aPath == aPrefix || aPath.StartsWith(aPrefix + "/", StringComparison.Ordinal);
    }
}
