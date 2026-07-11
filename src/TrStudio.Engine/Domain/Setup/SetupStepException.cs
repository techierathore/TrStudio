namespace TrStudio.Engine.Domain.Setup;

/// <summary>
/// Thrown by setup step handlers when a step fails with a message that is ACTIONABLE for the
/// admin (what went wrong + what to do), never a bare stack trace (REQ-FN-009 acceptance).
/// </summary>
public class SetupStepException : Exception
{
    /// <summary>
    /// Creates the exception with an actionable admin-facing message.
    /// </summary>
    /// <param name="aMessage">The actionable failure message.</param>
    public SetupStepException(string aMessage)
        : base(aMessage)
    {
    }

    /// <summary>
    /// Creates the exception with an actionable admin-facing message and the underlying cause.
    /// </summary>
    /// <param name="aMessage">The actionable failure message.</param>
    /// <param name="aInnerException">The underlying technical exception (logged, not surfaced).</param>
    public SetupStepException(string aMessage, Exception aInnerException)
        : base(aMessage, aInnerException)
    {
    }
}
