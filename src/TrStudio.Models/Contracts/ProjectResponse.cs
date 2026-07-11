namespace TrStudio.Models.Contracts;

/// <summary>
/// Response body describing a creator project/folder (REQ-FN-026).
/// </summary>
public class ProjectResponse
{
    /// <summary>The project identifier.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>The display name of the project.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional free-text description.</summary>
    public string? Description { get; set; }

    /// <summary>Whether the project is archived.</summary>
    public bool IsArchived { get; set; }

    /// <summary>UTC timestamp when the project was created.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>UTC timestamp when the project was last updated.</summary>
    public DateTime UpdatedUtc { get; set; }
}
