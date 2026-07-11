using TrStudio.Models.Enums;

namespace TrStudio.Models.Entities;

/// <summary>
/// One row of the full generation audit trail (SAFE-06 / BRD-65). EVERY generation — allowed OR
/// blocked, on every provider, at both the pre-check and post-check phase — writes an entry, so
/// any generation is traceable. Owner-scoped by the AppManager user id (BRD-81); read by the
/// admin audit surface.
/// </summary>
public class SafetyAuditEntry
{
    /// <summary>Unique audit-entry identifier.</summary>
    public Guid AuditId { get; set; }

    /// <summary>The job this decision relates to (may be a not-yet-persisted job on a pre-check block).</summary>
    public Guid? JobId { get; set; }

    /// <summary>The AppManager user id the generation belongs to (owner filter, BRD-81).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The registry model id the generation targeted.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>Which side of the generation was evaluated (input pre-check or output post-check).</summary>
    public SafetyPhase Phase { get; set; }

    /// <summary>Whether the generation was allowed through the floors.</summary>
    public bool Allowed { get; set; }

    /// <summary>The floor that rejected the generation, or <see cref="SafetyFloor.None"/> when allowed.</summary>
    public SafetyFloor Floor { get; set; }

    /// <summary>A short machine reason code (e.g. <c>no-consent</c>, <c>hash-match</c>, <c>classifier-error</c>).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>A human-readable detail of the decision (never the raw flagged content).</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the decision was made.</summary>
    public DateTime CreatedUtc { get; set; }
}
