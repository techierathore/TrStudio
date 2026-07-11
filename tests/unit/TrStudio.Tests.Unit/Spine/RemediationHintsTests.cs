using TrStudio.Engine.Domain.Jobs;

namespace TrStudio.Tests.Unit.Spine;

/// <summary>
/// Unit tests for <see cref="RemediationHints"/> (REQ-FN-005): every failure message maps to a
/// non-empty, actionable hint, and known failure families get targeted advice.
/// </summary>
public class RemediationHintsTests
{
    /// <summary>
    /// An out-of-memory style failure advises dropping resolution or routing to the High tier
    /// (the BRD-5 example remediation).
    /// </summary>
    [Fact]
    public void OutOfMemoryMapsToResolutionHint()
    {
        var vHint = RemediationHints.ForFailure("CUDA out of memory while rendering");

        Assert.Contains("256px", vHint);
        Assert.Contains("High tier", vHint);
    }

    /// <summary>
    /// An authentication failure points the admin at the provider key management screen.
    /// </summary>
    [Fact]
    public void AuthFailureMapsToKeyHint()
    {
        var vHint = RemediationHints.ForFailure("Provider returned 401 Unauthorized");

        Assert.Contains("API key", vHint);
    }

    /// <summary>
    /// Unknown failures still produce a non-empty generic hint — a failure is never surfaced
    /// without an action the creator can take.
    /// </summary>
    [Fact]
    public void UnknownFailureStillGetsHint()
    {
        Assert.False(string.IsNullOrWhiteSpace(RemediationHints.ForFailure("something exotic went wrong")));
        Assert.False(string.IsNullOrWhiteSpace(RemediationHints.ForFailure(null)));
    }
}
