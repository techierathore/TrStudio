using TrStudio.Models.Enums;

namespace TrStudio.Engine.Domain.Identity;

/// <summary>
/// Maps AppManager identity data (email, AppManager role, application role) onto TrStudio's
/// <see cref="UserRole"/>, and registration account types onto AppManager application role
/// codes (BRD-83/84). AppManager has three application roles, which every application maps to
/// its own (owner, 17 Nov 2026): <c>Admin</c> is Owner, <c>Manager</c> is Operator and
/// <c>User</c> is Creator. The older TrStudio names (Owner, Operator, Creator) are still read.
/// The generic superadmin (<c>admin@appmanager.local</c>) always maps to <see cref="UserRole.Owner"/>.
/// </summary>
public static class UserRoleMapper
{
    /// <summary>AppManager's application role that TrStudio reads as Owner.</summary>
    public const string OwnerRoleCode = "Admin";

    /// <summary>AppManager's application role that TrStudio reads as Operator; provisioned for operator accounts.</summary>
    public const string OperatorRoleCode = "Manager";

    /// <summary>AppManager's application role that TrStudio reads as Creator; provisioned for creator accounts.</summary>
    public const string CreatorRoleCode = "User";

    /// <summary>
    /// Resolves the TrStudio role for an authenticated AppManager user.
    /// </summary>
    /// <param name="aEmail">The user's email address.</param>
    /// <param name="aAppManagerRole">The AppManager-level role (e.g. "SuperAdmin", "ApplicationUser").</param>
    /// <param name="aApplicationRole">The application-scoped role code (e.g. "Creator", "Operator").</param>
    /// <param name="aSuperAdminEmail">The configured superadmin bootstrap email.</param>
    /// <returns>The resolved <see cref="UserRole"/> (defaults to Creator).</returns>
    public static UserRole MapRole(string? aEmail, string? aAppManagerRole, string? aApplicationRole, string aSuperAdminEmail)
    {
        if (!string.IsNullOrWhiteSpace(aEmail) && string.Equals(aEmail, aSuperAdminEmail, StringComparison.OrdinalIgnoreCase))
        {
            return UserRole.Owner;
        }

        if (IsAnyOf(aAppManagerRole, "SuperAdmin", "SystemAdmin", "Owner") || IsAnyOf(aApplicationRole, OwnerRoleCode, "Owner"))
        {
            return UserRole.Owner;
        }

        if (IsAnyOf(aApplicationRole, OperatorRoleCode, "Operator", "AdminOperator", "Admin-Operator"))
        {
            return UserRole.Operator;
        }

        return UserRole.Creator;
    }

    /// <summary>
    /// Maps a registration account type label onto the AppManager application role code to
    /// provision: AppManager's "User" (Creator) by default; operator variants map to "Manager" (Operator).
    /// </summary>
    /// <param name="aAccountType">The requested account type label from the register form.</param>
    /// <returns>The AppManager <c>applicationRoleCode</c> to send with the register call.</returns>
    public static string MapAccountTypeToRoleCode(string? aAccountType)
    {
        if (string.IsNullOrWhiteSpace(aAccountType))
        {
            return CreatorRoleCode;
        }

        var vNormalized = aAccountType.Trim();
        var vIsOperator = vNormalized.Contains("operator", StringComparison.OrdinalIgnoreCase)
            || vNormalized.Equals("admin", StringComparison.OrdinalIgnoreCase);
        return vIsOperator ? OperatorRoleCode : CreatorRoleCode;
    }

    /// <summary>
    /// Splits a display name into the first/last name pair AppManager registration requires.
    /// </summary>
    /// <param name="aName">The display name entered on the register form.</param>
    /// <returns>The first name and last name (last name falls back to the first when absent).</returns>
    public static (string FirstName, string LastName) SplitDisplayName(string aName)
    {
        var vTrimmed = (aName ?? string.Empty).Trim();
        var vSpaceIndex = vTrimmed.IndexOf(' ', StringComparison.Ordinal);
        if (vSpaceIndex <= 0)
        {
            return (vTrimmed, vTrimmed);
        }

        return (vTrimmed[..vSpaceIndex], vTrimmed[(vSpaceIndex + 1)..].Trim());
    }

    private static bool IsAnyOf(string? aValue, params string[] aCandidates)
    {
        return !string.IsNullOrWhiteSpace(aValue)
            && aCandidates.Any(aCandidate => string.Equals(aValue, aCandidate, StringComparison.OrdinalIgnoreCase));
    }
}
