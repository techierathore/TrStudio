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
    /// Application roles Operator/Admin map to the Operator role.
    /// </summary>
    [Theory]
    [InlineData("Operator")]
    [InlineData("Admin")]
    [InlineData("Admin-Operator")]
    public void OperatorLikeApplicationRoleMapsToOperator(string aApplicationRole)
    {
        var vRole = UserRoleMapper.MapRole("ops@example.com", "ApplicationUser", aApplicationRole, SuperAdminEmail);
        Assert.Equal(UserRole.Operator, vRole);
    }

    /// <summary>
    /// Any unrecognised or missing role data defaults to Creator (least privilege for studio use).
    /// </summary>
    [Theory]
    [InlineData("User")]
    [InlineData("Creator")]
    [InlineData(null)]
    public void UnknownRoleDefaultsToCreator(string? aApplicationRole)
    {
        var vRole = UserRoleMapper.MapRole("user@example.com", "ApplicationUser", aApplicationRole, SuperAdminEmail);
        Assert.Equal(UserRole.Creator, vRole);
    }

    /// <summary>
    /// Registration account types map to the AppManager application role code to provision:
    /// admin/operator variants → Operator, everything else → Creator.
    /// </summary>
    [Theory]
    [InlineData("Creator", "Creator")]
    [InlineData("Admin-Operator", "Operator")]
    [InlineData("operator", "Operator")]
    [InlineData("admin", "Operator")]
    [InlineData("", "Creator")]
    [InlineData(null, "Creator")]
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
