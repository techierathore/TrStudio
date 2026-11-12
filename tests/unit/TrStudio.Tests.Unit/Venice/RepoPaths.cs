namespace TrStudio.Tests.Unit.Venice;

/// <summary>Locates the repository root (the folder holding <c>TrStudio.slnx</c>) above the test assembly.</summary>
public static class RepoPaths
{
    /// <summary>The repository root.</summary>
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        for (var vDir = new DirectoryInfo(AppContext.BaseDirectory); vDir is not null; vDir = vDir.Parent)
        {
            if (File.Exists(Path.Combine(vDir.FullName, "TrStudio.slnx")))
            {
                return vDir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root (TrStudio.slnx) above the test assembly.");
    }
}
