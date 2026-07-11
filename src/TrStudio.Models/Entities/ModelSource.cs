using TrStudio.Models.Enums;

namespace TrStudio.Models.Entities;

/// <summary>
/// An external model repository configured by an admin as a download source for model installs.
/// </summary>
public class ModelSource
{
    /// <summary>Unique source identifier.</summary>
    public Guid SourceId { get; set; }

    /// <summary>Display name of the source.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The kind of repository this source points at.</summary>
    public ModelSourceKind Kind { get; set; }

    /// <summary>Base URL of the repository.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Whether an API token is stored (encrypted) for this source. The token itself is never exposed.</summary>
    public bool HasToken { get; set; }

    /// <summary>Whether the source is enabled for installs.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Connectivity/health status label (e.g. "ok", "unauthorized", "unreachable").</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the source was created.</summary>
    public DateTime CreatedUtc { get; set; }
}
