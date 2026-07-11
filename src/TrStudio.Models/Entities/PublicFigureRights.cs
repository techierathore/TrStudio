namespace TrStudio.Models.Entities;

/// <summary>
/// A recorded rights record permitting the depiction/impersonation of a named public figure
/// (SAFE-05 / BRD-64). Public-figure impersonation is BLOCKED by default; a generation is allowed
/// only when a matching, explicit rights record exists. Owner-scoped by the AppManager user id
/// (BRD-81); part of the audit trail (SAFE-06).
/// </summary>
public class PublicFigureRights
{
    /// <summary>Unique rights-record identifier.</summary>
    public Guid RightsId { get; set; }

    /// <summary>The AppManager user id that recorded the rights (owner filter, BRD-81).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>A stable reference identifying the public figure the rights cover.</summary>
    public string SubjectReference { get; set; } = string.Empty;

    /// <summary>The kind of rights evidence (e.g. <c>licence</c>, <c>signed-agreement</c>).</summary>
    public string RightsType { get; set; } = string.Empty;

    /// <summary>A description, URI or hash pointing at the stored rights evidence.</summary>
    public string RightsDetail { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the rights record was created.</summary>
    public DateTime CreatedUtc { get; set; }
}
