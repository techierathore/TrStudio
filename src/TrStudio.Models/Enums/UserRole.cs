namespace TrStudio.Models.Enums;

/// <summary>
/// The application role of an authenticated user, derived from AppManager identity data.
/// </summary>
public enum UserRole
{
    /// <summary>The instance owner; full admin rights including owner-only toggles.</summary>
    Owner,

    /// <summary>A creator using the TrStudio studio surfaces.</summary>
    Creator,

    /// <summary>An operator with management-app access but no owner-only rights.</summary>
    Operator
}
