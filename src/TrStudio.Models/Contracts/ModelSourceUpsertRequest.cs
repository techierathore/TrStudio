using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for creating or updating a model source (admin-scoped).
/// </summary>
/// <remarks>
/// <see cref="ApiToken"/> is write-only: it is stored encrypted in the key store and is
/// never returned by any read endpoint — reads expose only <see cref="Entities.ModelSource.HasToken"/>.
/// </remarks>
public class ModelSourceUpsertRequest
{
    /// <summary>The source to update; null/empty creates a new source.</summary>
    public Guid? SourceId { get; set; }

    /// <summary>Display name of the source.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The kind of repository this source points at.</summary>
    public ModelSourceKind Kind { get; set; }

    /// <summary>Base URL of the repository.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Write-only API token for the source; stored encrypted, never echoed back.</summary>
    public string? ApiToken { get; set; }

    /// <summary>Whether the source is enabled for installs.</summary>
    public bool IsEnabled { get; set; }
}
