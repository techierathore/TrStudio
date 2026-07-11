using TrStudio.Engine.Domain.Identity;
using TrStudio.Models.Enums;

namespace TrStudio.Tests.Unit.Identity;

/// <summary>
/// Tests for <see cref="RoleLandingMap"/>: the role → default landing table (BRD-84) and its
/// setup-incomplete variants (BRD-85) — nobody lands on a blank page.
/// </summary>
public class RoleLandingMapTests
{
    /// <summary>
    /// With setup complete: Owner and Creator land on the avatar studio, Operator lands on the
    /// ops dashboard — exactly the UsageGuide "Who lands where" table.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Owner, "/avatar")]
    [InlineData(UserRole.Creator, "/avatar")]
    [InlineData(UserRole.Operator, "/admin/ops")]
    public void SetupCompleteLandsOnRolePage(UserRole aRole, string aExpectedPath)
    {
        Assert.Equal(aExpectedPath, RoleLandingMap.GetLandingPath(aRole, true));
    }

    /// <summary>
    /// With setup incomplete: Owner is routed into guided setup, Operator into hardware
    /// readiness first, and Creator to the setup-required screen.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Owner, "/admin/setup")]
    [InlineData(UserRole.Operator, "/admin/setup/hardware")]
    [InlineData(UserRole.Creator, "/setup-required")]
    public void SetupIncompleteRoutesIntoSetup(UserRole aRole, string aExpectedPath)
    {
        Assert.Equal(aExpectedPath, RoleLandingMap.GetLandingPath(aRole, false));
    }
}
