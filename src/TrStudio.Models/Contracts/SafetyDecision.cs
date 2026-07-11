using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// The outcome of a safety evaluation. A block carries the floor that rejected the generation and
/// an actionable, non-revealing message; an allow carries <see cref="SafetyFloor.None"/>.
/// </summary>
public class SafetyDecision
{
    /// <summary>Whether the generation is permitted to proceed.</summary>
    public bool Allowed { get; init; }

    /// <summary>The floor that rejected the generation, or <see cref="SafetyFloor.None"/> when allowed.</summary>
    public SafetyFloor Floor { get; init; }

    /// <summary>A short machine reason code for the audit trail.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>A human-readable message safe to surface to the caller.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Builds an allow decision.</summary>
    /// <returns>An allow decision on <see cref="SafetyFloor.None"/>.</returns>
    public static SafetyDecision Allow()
        => new() { Allowed = true, Floor = SafetyFloor.None, Reason = "allowed", Message = "Allowed by the safety pipeline." };

    /// <summary>Builds a block decision.</summary>
    /// <param name="aFloor">The floor that rejected the generation.</param>
    /// <param name="aReason">A short machine reason code.</param>
    /// <param name="aMessage">A human-readable, non-revealing message.</param>
    /// <returns>A block decision.</returns>
    public static SafetyDecision Block(SafetyFloor aFloor, string aReason, string aMessage)
        => new() { Allowed = false, Floor = aFloor, Reason = aReason, Message = aMessage };
}
