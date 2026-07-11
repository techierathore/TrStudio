namespace TrStudioUI.Navigation;

/// <summary>
/// A labelled group of sidebar navigation items (e.g. Studios, Workspace, Models).
/// </summary>
/// <param name="Label">Group heading rendered as the sidebar group label.</param>
/// <param name="Items">Ordered navigation items in the group.</param>
public sealed record AppNavGroup(string Label, IReadOnlyList<AppNavItem> Items);
