namespace TrStudio.Models.Entities;

/// <summary>
/// Detail row for a single guided-setup step (persisted in the "SetupState" table),
/// rendered by the setup wizard UI and polled while a step runs.
/// </summary>
public class SetupStepInfo
{
    /// <summary>Stable step key (e.g. "OnnxRuntime", "PythonEnv", "ComfyUi").</summary>
    public string StepKey { get; set; } = string.Empty;

    /// <summary>Human-readable step name shown in the wizard.</summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>Step state label ("Pending", "Running", "Completed", "Failed", "Cancelled").</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>Progress of the step, 0-100.</summary>
    public int ProgressPercent { get; set; }

    /// <summary>Informational message for the current phase of the step, if any.</summary>
    public string? Message { get; set; }

    /// <summary>Actionable error message when the step failed, if any.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>UTC timestamp of the last state/progress update.</summary>
    public DateTime UpdatedUtc { get; set; }
}
