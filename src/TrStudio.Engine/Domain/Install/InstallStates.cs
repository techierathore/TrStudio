namespace TrStudio.Engine.Domain.Install;

/// <summary>
/// State labels persisted in "ModelInstall"."State" and surfaced in <c>ModelInstallItem.State</c>.
/// </summary>
public static class InstallStates
{
    /// <summary>Row created, download not started yet.</summary>
    public const string Queued = "queued";

    /// <summary>Download in flight.</summary>
    public const string Downloading = "downloading";

    /// <summary>Paused by the admin (or orphaned by a restart); the .partial file is kept for resume.</summary>
    public const string Paused = "paused";

    /// <summary>Download finished and the file was renamed into place.</summary>
    public const string Completed = "completed";

    /// <summary>Download failed; the error message is actionable and the .partial file is kept for resume.</summary>
    public const string Failed = "failed";

    /// <summary>Cancelled by the admin; the .partial file was deleted.</summary>
    public const string Cancelled = "cancelled";
}
