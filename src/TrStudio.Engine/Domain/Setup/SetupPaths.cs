namespace TrStudio.Engine.Domain.Setup;

/// <summary>
/// Resolves the managed-environment directory layout (REQ-FN-010). Every provisioning
/// operation derives its paths from here so nothing outside the managed root is ever touched.
/// </summary>
public class SetupPaths
{
    private readonly string objRoot;

    /// <summary>
    /// Initialises the path resolver from the setup options, expanding a leading <c>~</c>
    /// to the user profile directory cross-platform.
    /// </summary>
    /// <param name="aOptions">The setup options carrying <c>Setup:ManagedEnvRoot</c>.</param>
    public SetupPaths(SetupOptions aOptions)
    {
        objRoot = ExpandHome(aOptions.ManagedEnvRoot);
    }

    /// <summary>The absolute managed environment root (default <c>~/.trstudio/env</c>).</summary>
    public string Root => objRoot;

    /// <summary>Managed models directory (local install destination for small models).</summary>
    public string ModelsDir => Path.Combine(objRoot, "models");

    /// <summary>Managed cache directory (ONNX/tokenizer caches).</summary>
    public string CacheDir => Path.Combine(objRoot, "cache");

    /// <summary>Managed ONNX runtime working directory.</summary>
    public string OnnxDir => Path.Combine(objRoot, "onnx");

    /// <summary>Isolated Python installation directory (python-build-standalone unpack target).</summary>
    public string PythonDir => Path.Combine(objRoot, "python");

    /// <summary>Managed ComfyUI installation directory.</summary>
    public string ComfyUiDir => Path.Combine(objRoot, "comfyui");

    /// <summary>Download staging directory (.partial files live here during provisioning).</summary>
    public string DownloadsDir => Path.Combine(objRoot, "downloads");

    /// <summary>Directory holding locally-generated encryption key material.</summary>
    public string KeysDir => Path.Combine(objRoot, ".keys");

    /// <summary>The isolated Python executable path for the current OS.</summary>
    public string PythonExecutable => OperatingSystem.IsWindows()
        ? Path.Combine(PythonDir, "python.exe")
        : Path.Combine(PythonDir, "bin", "python3");

    /// <summary>
    /// Expands a leading <c>~</c> path segment to the current user's profile directory.
    /// </summary>
    /// <param name="aPath">The path, possibly starting with <c>~</c>.</param>
    /// <returns>The absolute expanded path.</returns>
    public static string ExpandHome(string aPath)
    {
        if (!aPath.StartsWith('~'))
        {
            return Path.GetFullPath(aPath);
        }

        var vHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var vTail = aPath.TrimStart('~').TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(vHome, vTail));
    }
}
