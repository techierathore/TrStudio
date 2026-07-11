namespace TrStudio.Engine.Domain.Setup;

/// <summary>
/// State labels persisted in "SetupState"."State" and surfaced in <c>SetupStepInfo.State</c>.
/// </summary>
public static class SetupStepStates
{
    /// <summary>The step has not run yet.</summary>
    public const string Pending = "Pending";

    /// <summary>The step is currently executing.</summary>
    public const string Running = "Running";

    /// <summary>The step completed successfully.</summary>
    public const string Completed = "Completed";

    /// <summary>The step failed; the error message is actionable.</summary>
    public const string Failed = "Failed";

    /// <summary>The step was cancelled by the admin; partial progress is kept for resume.</summary>
    public const string Cancelled = "Cancelled";
}
