namespace TrStudioUI.Navigation;

/// <summary>
/// A single sidebar navigation entry rendered by the shared app shell (REQ-UI-005).
/// </summary>
/// <param name="Label">Display label shown next to the icon.</param>
/// <param name="Href">Application-relative route the item links to (e.g. <c>/avatar</c>).</param>
/// <param name="Icon">TrBlazeUI <c>LucideIcon</c> name per the UIDesign icon map (e.g. <c>user</c>).</param>
/// <param name="ExtraMatchPrefixes">Additional route prefixes that also mark this item active (e.g. <c>/admin/setup</c> for the Hardware &amp; Setup item).</param>
public sealed record AppNavItem(string Label, string Href, string Icon, IReadOnlyList<string>? ExtraMatchPrefixes = null);
