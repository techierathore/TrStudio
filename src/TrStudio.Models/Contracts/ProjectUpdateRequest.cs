namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for updating a creator project's mutable fields
/// (<c>PUT /api/v1/projects/{id}</c>, REQ-FN-026).
/// </summary>
public class ProjectUpdateRequest
{
    /// <summary>The updated display name of the project.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The updated free-text description.</summary>
    public string? Description { get; set; }

    /// <summary>Whether the project is archived (hidden from default listings).</summary>
    public bool IsArchived { get; set; }
}
