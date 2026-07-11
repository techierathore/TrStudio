namespace TrStudio.Models.Entities;

/// <summary>
/// A creator project grouping jobs and assets in the workspace.
/// </summary>
public class Project
{
    /// <summary>Unique project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>The AppManager user id owning this project (per-user isolation, BRD-81).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Display name of the project.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional free-text description.</summary>
    public string? Description { get; set; }

    /// <summary>Whether the project is archived (hidden from default listings).</summary>
    public bool IsArchived { get; set; }

    /// <summary>UTC timestamp when the project was created.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>UTC timestamp when the project was last updated.</summary>
    public DateTime UpdatedUtc { get; set; }
}
