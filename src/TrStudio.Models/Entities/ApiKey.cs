namespace TrStudio.Models.Entities;

/// <summary>
/// An issued external REST API key (REQ-FN-069/BRD-66) mapping an <c>X-Api-Key</c> credential to
/// an owning AppManager user and scope, letting non-interactive callers reach the same API as an
/// interactive session. Only the SHA-256 <see cref="KeyHash"/> is persisted — the plaintext key is
/// returned to the owner exactly once at issue time and never stored (NFR-05).
/// </summary>
public class ApiKey
{
    /// <summary>Unique API-key identifier (the handle used to revoke).</summary>
    public Guid ApiKeyId { get; set; }

    /// <summary>The AppManager user id the key authenticates as (per-user isolation, BRD-81).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The creator-facing label for this key (e.g. "CI pipeline").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The non-secret display prefix of the key (e.g. <c>trsk_ab12cd34</c>) shown in listings.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>The lowercase hex SHA-256 hash of the full key — the only representation stored at rest.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>The authorization scope granted to callers using this key (e.g. <c>jobs</c>).</summary>
    public string Scope { get; set; } = "jobs";

    /// <summary>UTC timestamp when the key was issued.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>UTC timestamp the key was last used to authenticate, if ever.</summary>
    public DateTime? LastUsedUtc { get; set; }

    /// <summary>UTC timestamp the key was revoked; a revoked key never authenticates again.</summary>
    public DateTime? RevokedUtc { get; set; }
}
