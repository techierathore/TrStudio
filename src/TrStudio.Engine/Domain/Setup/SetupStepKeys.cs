namespace TrStudio.Engine.Domain.Setup;

/// <summary>
/// Stable keys of the guided-setup steps (rows of the "SetupState" table, seeded by script 0300).
/// </summary>
public static class SetupStepKeys
{
    /// <summary>Step 1 — verify/provision managed dirs and probe the ONNX runtime.</summary>
    public const string OnnxRuntime = "OnnxRuntime";

    /// <summary>Step 2 — provision the isolated python-build-standalone environment.</summary>
    public const string PythonEnv = "PythonEnv";

    /// <summary>Step 3 — provision managed ComfyUI and its pip dependencies.</summary>
    public const string ComfyUi = "ComfyUi";

    /// <summary>All step keys in execution order.</summary>
    public static readonly string[] Ordered = { OnnxRuntime, PythonEnv, ComfyUi };
}
