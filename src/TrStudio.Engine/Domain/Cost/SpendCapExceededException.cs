namespace TrStudio.Engine.Domain.Cost;

/// <summary>
/// Thrown by the orchestrator when a billed job's estimate would push month-to-date spend over
/// the configured monthly cap and <see cref="CostOptions.BlockOverCap"/> is enabled
/// (REQ-FN-029/BRD-13). The API surface maps it to an actionable 402 response.
/// </summary>
public class SpendCapExceededException : Exception
{
    /// <summary>
    /// Initialises the exception with an actionable, user-facing message.
    /// </summary>
    /// <param name="aMessage">The actionable message describing the cap breach and remediation.</param>
    public SpendCapExceededException(string aMessage) : base(aMessage)
    {
    }
}
