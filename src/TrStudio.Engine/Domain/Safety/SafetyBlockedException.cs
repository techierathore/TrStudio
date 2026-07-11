using TrStudio.Models.Enums;

namespace TrStudio.Engine.Domain.Safety;

/// <summary>
/// Thrown when the non-disableable safety pipeline blocks a submission at pre-check (SAFE-01..06).
/// The API surface translates this into a deliberate 4xx refusal (never a 500) — a block is the
/// policy working, not a server error. The offending job is persisted as
/// <see cref="JobState.Blocked"/> and audited before this is thrown.
/// </summary>
public class SafetyBlockedException : Exception
{
    /// <summary>
    /// Initialises the exception.
    /// </summary>
    /// <param name="aFloor">The floor that rejected the submission.</param>
    /// <param name="aMessage">The non-revealing block message surfaced to the caller.</param>
    public SafetyBlockedException(SafetyFloor aFloor, string aMessage) : base(aMessage)
    {
        Floor = aFloor;
    }

    /// <summary>The floor that rejected the submission.</summary>
    public SafetyFloor Floor { get; }
}
