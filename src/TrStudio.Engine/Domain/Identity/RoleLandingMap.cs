using TrStudio.Models.Enums;

namespace TrStudio.Engine.Domain.Identity;

/// <summary>
/// Role → default landing path map (BRD-84) combined with the first-run setup gate variants
/// (BRD-85), per the BRD §9 F-IDENTITY table and the UsageGuide "Who lands where" table.
/// Nobody ever lands on a blank page: every role/gate combination yields a concrete path.
/// </summary>
public static class RoleLandingMap
{
    /// <summary>Creator surface landing for Owners and Creators when setup is complete.</summary>
    public const string AvatarStudioPath = "/avatar";

    /// <summary>Operator landing when setup is complete (TrStudioAdmin ops dashboard).</summary>
    public const string OpsDashboardPath = "/admin/ops";

    /// <summary>Owner landing when setup is incomplete (guided setup wizard).</summary>
    public const string GuidedSetupPath = "/admin/setup";

    /// <summary>Operator landing when setup is incomplete (hardware &amp; readiness first).</summary>
    public const string HardwareSetupPath = "/admin/setup/hardware";

    /// <summary>Creator landing when setup is incomplete ("ask your admin" screen).</summary>
    public const string SetupRequiredPath = "/setup-required";

    /// <summary>
    /// Resolves the landing path for a role given the setup gate outcome.
    /// </summary>
    /// <param name="aRole">The user's application role.</param>
    /// <param name="aIsSetupComplete">Whether the first-run setup gate has passed.</param>
    /// <returns>The path the UI should route the user to after login.</returns>
    public static string GetLandingPath(UserRole aRole, bool aIsSetupComplete)
    {
        if (aIsSetupComplete)
        {
            return aRole == UserRole.Operator ? OpsDashboardPath : AvatarStudioPath;
        }

        return aRole switch
        {
            UserRole.Owner => GuidedSetupPath,
            UserRole.Operator => HardwareSetupPath,
            _ => SetupRequiredPath
        };
    }
}
