namespace TrStudio.Engine.Domain.Image;

/// <summary>
/// Raised by the local ONNX image path when no runnable model file is present and the synthetic
/// fallback is disabled. Carries an actionable remediation hint surfaced to the creator on the
/// failed job (REQ-FN-019, mirroring the StubEcho FAILME remediation path of REQ-FN-005).
/// </summary>
public class ModelNotInstalledException : Exception
{
    /// <summary>
    /// Initialises the exception with a failure message and an actionable remediation hint.
    /// </summary>
    /// <param name="aMessage">The human-readable failure message.</param>
    /// <param name="aRemediationHint">An actionable hint for the creator on how to recover.</param>
    public ModelNotInstalledException(string aMessage, string aRemediationHint)
        : base(aMessage)
    {
        RemediationHint = aRemediationHint;
    }

    /// <summary>An actionable hint for the creator on how to recover from the failure.</summary>
    public string RemediationHint { get; }
}
