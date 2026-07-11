namespace TrStudio.Models.Entities;

/// <summary>
/// Progress snapshot of the first-run environment setup (ONNX runtime, isolated Python
/// environment, managed ComfyUI) driven by the setup service.
/// </summary>
public class SetupStatus
{
    /// <summary>Whether the ONNX runtime image path is installed.</summary>
    public bool OnnxInstalled { get; set; }

    /// <summary>Whether the isolated Python environment is provisioned.</summary>
    public bool PythonEnvReady { get; set; }

    /// <summary>Whether the managed ComfyUI instance is installed and reachable.</summary>
    public bool ComfyUiReady { get; set; }

    /// <summary>Labels of the setup steps completed so far.</summary>
    public string[] StepsCompleted { get; set; } = Array.Empty<string>();

    /// <summary>The step currently executing, if setup is in progress.</summary>
    public string? CurrentStep { get; set; }

    /// <summary>Error message if the current/last step failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Persisted progress (0-100) of the currently executing step, if any.</summary>
    public int ProgressPercent { get; set; }

    /// <summary>Per-step detail rows for the setup wizard UI.</summary>
    public SetupStepInfo[] Steps { get; set; } = Array.Empty<SetupStepInfo>();
}
