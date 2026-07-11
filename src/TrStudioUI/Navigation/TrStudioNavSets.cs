namespace TrStudioUI.Navigation;

/// <summary>
/// Factory for the two navigation surfaces of the one unified TrStudio app (ADR-012): the
/// creator studios and the (role-gated) management surface. Both are registered together and
/// <see cref="AppShellNavigation"/> picks the active one from the signed-in user's role.
/// </summary>
public static class TrStudioNavSets
{
    /// <summary>Default landing route for the creator surface (matches the login role landing).</summary>
    public const string CreatorLandingPath = "/avatar";

    /// <summary>Default landing route for the management surface (matches the Operator role landing).</summary>
    public const string ManagementLandingPath = "/admin/ops";

    /// <summary>
    /// Builds the creator-studios navigation (Studios + Workspace groups).
    /// </summary>
    /// <returns>The creator surface navigation configuration.</returns>
    public static AppNavigationConfig CreateCreatorNavigation()
    {
        var vGroups = new List<AppNavGroup>
        {
            new("Studios", new List<AppNavItem>
            {
                new("Avatar Studio", "/avatar", "user"),
                new("Characters", "/avatar/characters", "users"),
                new("Image Studio", "/image", "image"),
                new("Video Studio", "/video", "video"),
                new("Voice Studio", "/voice", "mic"),
                new("Prompt Studio", "/prompt-studio", "message-square"),
            }),
            new("Workspace", new List<AppNavItem>
            {
                new("Gallery", "/gallery", "layout-grid"),
                new("Projects", "/projects", "folder"),
                new("Library", "/library", "book-open"),
                new("Account", "/account", "circle-user"),
            }),
        };

        return new AppNavigationConfig("TrStudio", null, vGroups);
    }

    /// <summary>
    /// Builds the management-surface navigation (Environment + Models + Governance groups)
    /// hosted under the role-gated <c>/admin/*</c> routes.
    /// </summary>
    /// <returns>The management surface navigation configuration.</returns>
    public static AppNavigationConfig CreateManagementNavigation()
    {
        var vGroups = new List<AppNavGroup>
        {
            new("Environment", new List<AppNavItem>
            {
                new("Hardware & Setup", "/admin/setup/hardware", "cpu", new[] { "/admin/setup" }),
                new("RunPod & Ops", "/admin/ops", "cloud"),
            }),
            new("Models", new List<AppNavItem>
            {
                new("Model Sources", "/admin/sources", "link"),
                new("Model Library", "/admin/models", "package"),
                new("Registry & Providers", "/admin/registry", "database"),
                new("ComfyUI Import", "/admin/comfy/import", "workflow"),
            }),
            new("Governance", new List<AppNavItem>
            {
                new("Content Policy", "/admin/policy", "shield"),
            }),
        };

        return new AppNavigationConfig("TrStudio", "admin", vGroups);
    }
}
