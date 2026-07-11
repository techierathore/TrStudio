using TrStudio.Engine.Domain.Setup;

namespace TrStudio.Engine.Domain.Install;

/// <summary>
/// Decides where a model install lands (REQ-FN-011 acceptance): an explicit destination always
/// wins; otherwise downloads larger than 10 GB default to the RunPod network-volume root and
/// everything else to the managed local models directory.
/// </summary>
public class InstallDestinationPlanner
{
    private readonly SetupOptions objOptions;
    private readonly SetupPaths objPaths;

    /// <summary>
    /// Initialises the planner from the setup options.
    /// </summary>
    /// <param name="aOptions">The setup options carrying the network-volume root.</param>
    /// <param name="aPaths">The managed-environment path resolver.</param>
    public InstallDestinationPlanner(SetupOptions aOptions, SetupPaths aPaths)
    {
        objOptions = aOptions;
        objPaths = aPaths;
    }

    /// <summary>
    /// Plans the destination directory for a download.
    /// </summary>
    /// <param name="aTotalBytes">The probed download size in bytes, if known.</param>
    /// <param name="aRequestedDestination">An explicit destination directory, or null/empty for the default rule.</param>
    /// <returns>The destination directory the model file is installed into.</returns>
    public string PlanDestination(long? aTotalBytes, string? aRequestedDestination)
    {
        if (!string.IsNullOrWhiteSpace(aRequestedDestination))
        {
            return aRequestedDestination;
        }

        var vIsLarge = aTotalBytes.HasValue && aTotalBytes.Value > SetupOptions.NetworkVolumeThresholdBytes;
        if (vIsLarge && !string.IsNullOrWhiteSpace(objOptions.NetworkVolumeRoot))
        {
            return objOptions.NetworkVolumeRoot;
        }

        return objPaths.ModelsDir;
    }
}
