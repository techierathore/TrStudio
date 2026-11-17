using TrStudio.Engine.Domain.Identity;
using TrStudio.Models.Enums;

namespace TrStudio.Tests.Unit.Identity;

/// <summary>
/// Tests for <see cref="UserRoleMapper"/>: AppManager identity → TrStudio role mapping,
/// including the superadmin bootstrap (BRD-83) and account-type provisioning.
/// </summary>
public class UserRoleMapperTests
{
    private const string SuperAdminEmail = "admin@appmanager.local";

    /// <summary>
    /// The pre-existing generic superadmin email maps to Owner regardless of the roles
    /// AppManager reports (case-insensitively) — the superadmin bootstrap of REQ-FN-015.
    /// </summary>
    [Fact]
    public void SuperAdminEmailMapsToOwner()
    {
        var vRole = UserRoleMapper.MapRole("Admin@AppManager.local", "ApplicationUser", "User", SuperAdminEmail);
        Assert.Equal(UserRole.Owner, vRole);
    }

    /// <summary>
    /// An AppManager-level SuperAdmin role maps to Owner even under a different email.
    /// </summary>
    [Fact]
    public void AppManagerSuperAdminRoleMapsToOwner()
    {
        var vRole = UserRoleMapper.MapRole("root@example.com", "SuperAdmin", null, SuperAdminEmail);
        Assert.Equal(UserRole.Owner, vRole);
    }

    /// <summary>
    /// AppManager's three application roles map to TrStudio's own (owner, 17 Nov 2026):
    /// Admin is Owner, Manager is Operator, User is Creator. Test user 1 is an Admin and test
    /// user 2 a Manager on AppManager; both used to land as Operators.
    /// </summary>
    [Theory(DisplayName = "REQ-FN-115 AppManagerRolesMapToTrStudioRoles")]
    [InlineData("Admin", UserRole.Owner)]
    [InlineData("Manager", UserRole.Operator)]
    [InlineData("User", UserRole.Creator)]
    [InlineData("admin", UserRole.Owner)]
    [InlineData("MANAGER", UserRole.Operator)]
    public void AppManagerRolesMapToTrStudioRoles(string aApplicationRole, UserRole aExpected)
    {
        var vRole = UserRoleMapper.MapRole("someone@example.com", "ApplicationUser", aApplicationRole, SuperAdminEmail);
        Assert.Equal(aExpected, vRole);
    }

    /// <summary>
    /// The older TrStudio role names are still read the same way.
    /// </summary>
    [Theory]
    [InlineData("Owner", UserRole.Owner)]
    [InlineData("Operator", UserRole.Operator)]
    [InlineData("Admin-Operator", UserRole.Operator)]
    [InlineData("Creator", UserRole.Creator)]
    public void OlderTrStudioRoleNamesStillMap(string aApplicationRole, UserRole aExpected)
    {
        var vRole = UserRoleMapper.MapRole("someone@example.com", "ApplicationUser", aApplicationRole, SuperAdminEmail);
        Assert.Equal(aExpected, vRole);
    }

    /// <summary>
    /// Missing or unknown role data defaults to Creator (least privilege for studio use).
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Guest")]
    public void UnknownRoleDefaultsToCreator(string? aApplicationRole)
    {
        var vRole = UserRoleMapper.MapRole("user@example.com", "ApplicationUser", aApplicationRole, SuperAdminEmail);
        Assert.Equal(UserRole.Creator, vRole);
    }

    /// <summary>
    /// Registration provisions AppManager's own role codes: an operator account gets Manager,
    /// everything else gets User (Creator).
    /// </summary>
    [Theory]
    [InlineData("Creator", "User")]
    [InlineData("AdminOperator", "Manager")]
    [InlineData("Admin-Operator", "Manager")]
    [InlineData("operator", "Manager")]
    [InlineData("admin", "Manager")]
    [InlineData("", "User")]
    [InlineData(null, "User")]
    public void AccountTypeMapsToRoleCode(string? aAccountType, string aExpectedCode)
    {
        Assert.Equal(aExpectedCode, UserRoleMapper.MapAccountTypeToRoleCode(aAccountType));
    }

    /// <summary>
    /// Display names split into first/last for AppManager registration; a single-word name is
    /// reused as the last name because AppManager requires both.
    /// </summary>
    [Theory]
    [InlineData("Jane Doe", "Jane", "Doe")]
    [InlineData("Jane Van Der Berg", "Jane", "Van Der Berg")]
    [InlineData("Prince", "Prince", "Prince")]
    public void DisplayNameSplitsIntoFirstAndLast(string aName, string aExpectedFirst, string aExpectedLast)
    {
        var (vFirst, vLast) = UserRoleMapper.SplitDisplayName(aName);
        Assert.Equal(aExpectedFirst, vFirst);
        Assert.Equal(aExpectedLast, vLast);
    }
}
