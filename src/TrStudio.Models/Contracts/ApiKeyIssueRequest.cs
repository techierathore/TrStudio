namespace TrStudio.Models.Contracts;

/// <summary>
/// Body to issue a new external API key (REQ-FN-069/BRD-66).
/// </summary>
public class ApiKeyIssueRequest
{
    /// <summary>A creator-facing label for the key (e.g. "CI pipeline"); required.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The scope to grant (optional; defaults to <c>jobs</c>).</summary>
    public string? Scope { get; set; }
}
