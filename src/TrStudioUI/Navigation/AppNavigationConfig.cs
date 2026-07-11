namespace TrStudioUI.Navigation;

/// <summary>
/// A single surface's navigation configuration consumed by the shared app shell (REQ-UI-005).
/// The unified app registers two of these — the creator surface and the management surface —
/// and <see cref="AppShellNavigation"/> selects the active one from the signed-in user's role
/// (ADR-012 / REQ-UI-080).
/// </summary>
/// <param name="AppName">App display name shown in the sidebar header and breadcrumb root.</param>
/// <param name="AppBadge">Optional small badge next to the app name (e.g. <c>admin</c>); null hides it.</param>
/// <param name="Groups">Ordered sidebar navigation groups.</param>
public sealed record AppNavigationConfig(
    string AppName,
    string? AppBadge,
    IReadOnlyList<AppNavGroup> Groups);
