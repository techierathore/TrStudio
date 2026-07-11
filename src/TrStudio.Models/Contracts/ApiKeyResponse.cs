namespace TrStudio.Models.Contracts;

/// <summary>
/// A non-secret view of an issued API key (REQ-FN-069/BRD-66) for the owner's key-management list.
/// The full secret is never included — only the display prefix and metadata.
/// </summary>
public class ApiKeyResponse
{
    /// <summary>Unique API-key identifier (the handle used to revoke).</summary>
    public Guid ApiKeyId { get; set; }

    /// <summary>The creator-facing label for this key.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The non-secret display prefix (e.g. <c>trsk_ab12cd34</c>).</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>The authorization scope granted to callers using this key.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the key was issued.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>UTC timestamp the key was last used to authenticate, if ever.</summary>
    public DateTime? LastUsedUtc { get; set; }

    /// <summary>UTC timestamp the key was revoked, if it has been.</summary>
    public DateTime? RevokedUtc { get; set; }

    /// <summary>Whether the key is still active (not revoked).</summary>
    public bool IsActive => RevokedUtc is null;
}
