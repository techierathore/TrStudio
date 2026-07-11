namespace TrStudio.Engine.Domain.Jobs;

/// <summary>
/// Maps provider failure messages to actionable remediation hints surfaced to the UI
/// (REQ-FN-005/BRD-5: failures must tell the creator what to do next, not just what broke).
/// </summary>
public static class RemediationHints
{
    /// <summary>
    /// Produces an actionable remediation hint for a failure message.
    /// </summary>
    /// <param name="aFailureMessage">The provider/worker failure description, if any.</param>
    /// <returns>A non-empty, user-actionable hint.</returns>
    public static string ForFailure(string? aFailureMessage)
    {
        var vMessage = aFailureMessage ?? string.Empty;

        if (Contains(vMessage, "memory") || Contains(vMessage, "vram") || Contains(vMessage, "oom"))
        {
            return "Drop the resolution to 256px or route the job to the High tier.";
        }

        if (Contains(vMessage, "timeout") || Contains(vMessage, "unreachable") || Contains(vMessage, "connect"))
        {
            return "Check that the provider endpoint is reachable and retry; if it persists, switch the job to another tier.";
        }

        if (Contains(vMessage, "unauthorized") || Contains(vMessage, "api key") || Contains(vMessage, "401") || Contains(vMessage, "forbidden"))
        {
            return "Verify the provider API key under Admin > Providers and make sure it is enabled.";
        }

        if (Contains(vMessage, "disk") || Contains(vMessage, "space"))
        {
            return "Free disk space under the assets root or move Storage:AssetsRoot to a larger drive, then retry.";
        }

        return "Retry the job; if it keeps failing, pick a different model or tier from the model list.";
    }

    private static bool Contains(string aMessage, string aFragment)
        => aMessage.Contains(aFragment, StringComparison.OrdinalIgnoreCase);
}
