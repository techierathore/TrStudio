namespace TrStudioUI.Navigation;

/// <summary>
/// The two role-gated surfaces of the unified TrStudio app (ADR-012 / REQ-UI-080): the
/// creator studios and the management/admin surface. An Owner may switch between them in-app;
/// a Creator only sees <see cref="Creator"/> and an Operator only <see cref="Management"/>.
/// </summary>
public enum AppSurface
{
    /// <summary>The creator studios (avatar/image/video/voice/gallery/projects/account).</summary>
    Creator,

    /// <summary>The management surface (registry, ops, sources, models, setup, comfy import, policy).</summary>
    Management,
}
