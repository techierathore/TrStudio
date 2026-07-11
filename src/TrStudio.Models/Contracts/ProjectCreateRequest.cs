namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for creating a creator project/folder (<c>POST /api/v1/projects</c>, REQ-FN-026).
/// </summary>
public class ProjectCreateRequest
{
    /// <summary>The display name of the project.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional free-text description.</summary>
    public string? Description { get; set; }
}
